using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// GitHub adaptation of practice 2.0. Does not connect to Azure or need a PAT.
const string seedLabel = "demo:parts-unlimited";
var templatePath = Path.Combine(AppContext.BaseDirectory, "template.json");
var template = JsonNode.Parse(await File.ReadAllTextAsync(templatePath))!.AsObject();
var items = template["items"]!.AsArray().Select(x => x!.AsObject()).ToList();
var links = template["links"]!.AsArray().Select(x => x!.AsObject()).ToList();
var iterations = template["iterations"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
var ids = items.Select(x => x["sourceId"]!.GetValue<int>()).ToHashSet();
if (ids.Count != items.Count || links.Any(x => !ids.Contains(x["parent"]!.GetValue<int>()) || !ids.Contains(x["child"]!.GetValue<int>())))
    throw new InvalidDataException("Duplicate IDs or dangling hierarchy links in template.");
if (items.Any(x => x["iteration"] is not null && !iterations.Contains(x["iteration"]!.GetValue<string>())))
    throw new InvalidDataException("Unknown iteration in template.");
Console.WriteLine($"Parts Unlimited: {items.Count} items, {links.Count} hierarchy links, {iterations.Count} sprints.");
foreach (var group in items.GroupBy(x => x["type"]!.GetValue<string>()))
    Console.WriteLine($"  {group.Key}: {group.Count()}");
if (args.Length == 0 || args.SequenceEqual(new[] { "--dry-run" }))
{
    Console.WriteLine("Template validated. No remote changes. Use --apply to import.");
    return;
}
if (!args.SequenceEqual(new[] { "--apply" })) throw new ArgumentException("Use --dry-run or --apply.");
var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "";
if (!Regex.IsMatch(repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))
    throw new InvalidOperationException("GITHUB_REPOSITORY must be owner/repo.");
var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? throw new InvalidOperationException("Missing GITHUB_TOKEN.");
using var client = new HttpClient { BaseAddress = new Uri("https://api.github.com/"), Timeout = TimeSpan.FromSeconds(60) };
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
client.DefaultRequestHeaders.UserAgent.ParseAdd("StudyFlow-DemoGenerator/1.0");
client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
var prefix = $"repos/{repository}/";
DateTimeOffset lastMutation = DateTimeOffset.MinValue;

async Task<JsonNode> Call(HttpMethod method, string path, object? body = null)
{
    // Serial writes stay below the content creation limit. Never log the token.
    for (var attempt = 0; ; attempt++)
    {
        if (method != HttpMethod.Get)
        {
            var wait = TimeSpan.FromSeconds(1.1) - (DateTimeOffset.UtcNow - lastMutation);
            if (wait > TimeSpan.Zero) await Task.Delay(wait);
            lastMutation = DateTimeOffset.UtcNow;
        }
        using var request = new HttpRequestMessage(method, prefix + path);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        if (attempt < 3 && (response.StatusCode == HttpStatusCode.TooManyRequests ||
            (response.StatusCode == HttpStatusCode.Forbidden && (response.Headers.RetryAfter is not null || text.Contains("rate limit", StringComparison.OrdinalIgnoreCase)))))
        {
            await Task.Delay(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(65 * (attempt + 1)));
            continue;
        }
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{method} {path}: {(int)response.StatusCode}: {text}");
        return JsonNode.Parse(text) ?? new JsonObject();
    }
}
async Task<List<JsonObject>> List(string path)
{
    var rows = new List<JsonObject>();
    for (var page = 1; ; page++)
    {
        var batch = (await Call(HttpMethod.Get, $"{path}{(path.Contains('?') ? '&' : '?')}per_page=100&page={page}")).AsArray();
        rows.AddRange(batch.Select(x => x!.AsObject()));
        if (batch.Count < 100) return rows;
    }
}
string TypeLabel(JsonObject item) => "type:" + item["type"]!.GetValue<string>().ToLowerInvariant().Replace(' ', '-');
string StateLabel(JsonObject item) => "state:" + item["state"]!.GetValue<string>().ToLowerInvariant().Replace(' ', '-');
string Marker(int id) => $"<!-- parts-unlimited:{id} -->";

var desiredLabels = items.Select(TypeLabel).Concat(items.Select(StateLabel)).Append(seedLabel).Distinct().ToList();
var existingLabels = (await List("labels")).Select(x => x["name"]!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
foreach (var label in desiredLabels.Where(x => !existingLabels.Contains(x)))
    await Call(HttpMethod.Post, "labels", new { name = label, color = label.StartsWith("type:") ? "8250df" : label.StartsWith("state:") ? "0e8a16" : "0969da", description = "Parts Unlimited educational demo data (practice 2.0)" });

var milestones = (await List("milestones?state=all")).ToDictionary(x => x["title"]!.GetValue<string>(), x => x["number"]!.GetValue<int>());
var sprintStart = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);
for (var index = 0; index < iterations.Count; index++)
{
    var name = iterations[index];
    if (milestones.ContainsKey(name)) continue;
    var start = sprintStart.AddDays(index * 14);
    var end = start.AddDays(14).AddSeconds(-1);
    var milestone = await Call(HttpMethod.Post, "milestones", new {
        title = name, description = $"Учебный спринт {start:yyyy-MM-dd} — {end:yyyy-MM-dd}. Даты адаптированы для практик; не реальное расписание.",
        due_on = end.ToString("yyyy-MM-ddTHH:mm:ssZ")
    });
    milestones[name] = milestone["number"]!.GetValue<int>();
}

var imported = new Dictionary<int, JsonObject>();
foreach (var issue in await List($"issues?state=all&labels={Uri.EscapeDataString(seedLabel)}"))
{
    if (issue.ContainsKey("pull_request")) continue;
    var match = Regex.Match(issue["body"]?.GetValue<string>() ?? "", @"<!-- parts-unlimited:(\d+) -->");
    if (match.Success && !imported.TryAdd(int.Parse(match.Groups[1].Value), issue))
        throw new InvalidDataException("Duplicate seed marker: " + match.Value);
}
var created = 0;
var createdIds = new HashSet<int>();
foreach (var item in items)
{
    var id = item["sourceId"]!.GetValue<int>();
    if (imported.ContainsKey(id)) continue; // Preserve edits made in later practices.
    var state = item["state"]!.GetValue<string>();
    var iteration = item["iteration"]?.GetValue<string>();
    var description = item["description"]!.GetValue<string>();
    var body = $"{Marker(id)}\n\n> Тестовая карточка Parts Unlimited, импортированная для практики 2.0. Статус из шаблона не означает реализацию функции StudyFlow.\n\n" +
        $"| Поле | Значение |\n|---|---|\n| Source ID | {id} |\n| Type | {item["type"]} |\n| Original state | {state} |\n| Iteration | {iteration ?? "Backlog"} |\n| Effort | {item["effort"]} |\n| Remaining Work (hours) | {item["remainingWork"]} |\n| Priority | {item["priority"]} |\n\n{description}\n";
    var steps = item["testSteps"]!.GetValue<string>();
    if (steps.Length > 0) body += "\n<details><summary>Original test steps (Azure XML)</summary>\n\n```xml\n" + steps + "\n```\n</details>\n";
    body += $"\nИсточник: [Microsoft / AzDevOpsDemoGenerator]({template["sourceRepository"]}/tree/{template["sourceCommit"]}/{template["sourcePath"]}), MIT.\n";
    var payload = new JsonObject { ["title"] = item["title"]!.GetValue<string>(), ["body"] = body,
        ["labels"] = new JsonArray(seedLabel, TypeLabel(item), StateLabel(item)) };
    if (iteration is not null) payload["milestone"] = milestones[iteration];
    var issue = (await Call(HttpMethod.Post, "issues", payload)).AsObject();
    imported.Add(id, issue);
    createdIds.Add(id);
    created++;
    Console.WriteLine($"Created #{issue["number"]}: {item["title"]}");
}

var linked = 0;
foreach (var group in links.GroupBy(x => x["parent"]!.GetValue<int>()))
{
    var parentNumber = imported[group.Key]["number"]!.GetValue<int>();
    var children = (await List($"issues/{parentNumber}/sub_issues")).Select(x => x["id"]!.GetValue<long>()).ToHashSet();
    foreach (var link in group)
    {
        var childId = imported[link["child"]!.GetValue<int>()]["id"]!.GetValue<long>();
        if (!children.Contains(childId)) await Call(HttpMethod.Post, $"issues/{parentNumber}/sub_issues", new { sub_issue_id = childId });
        linked++;
    }
}
// Close only fresh demo cards; subsequent runs must not overwrite learning edits.
foreach (var item in items.Where(x => x["state"]!.GetValue<string>() == "Done" && createdIds.Contains(x["sourceId"]!.GetValue<int>())))
{
    var issue = imported[item["sourceId"]!.GetValue<int>()];
    await Call(HttpMethod.Patch, $"issues/{issue["number"]}", new { state = "closed", state_reason = "completed" });
}
var verified = await List($"issues?state=all&labels={Uri.EscapeDataString(seedLabel)}");
var verifiedIds = verified.Select(x => Regex.Match(x["body"]?.GetValue<string>() ?? "", @"<!-- parts-unlimited:(\d+) -->"))
    .Where(x => x.Success).Select(x => int.Parse(x.Groups[1].Value)).ToList();
if (verifiedIds.Count != ids.Count || !ids.SetEquals(verifiedIds)) throw new InvalidDataException("Remote verification failed.");
var report = new JsonObject {
    ["repository"] = repository, ["sourceCommit"] = template["sourceCommit"]!.DeepClone(),
    ["items"] = imported.Count, ["createdThisRun"] = created, ["hierarchyLinks"] = linked, ["sprints"] = iterations.Count,
    ["mapping"] = new JsonArray(imported.OrderBy(x => x.Key).Select(x => (JsonNode)new JsonObject {
        ["sourceId"] = x.Key, ["number"] = x.Value["number"]!.DeepClone(), ["url"] = x.Value["html_url"]!.DeepClone()
    }).ToArray())
};
await File.WriteAllTextAsync("seed-result.json", report.ToJsonString(new() { WriteIndented = true }));
var summary = $"## Практика 2.0 — Parts Unlimited\n\nПроверено: **{imported.Count} карточек**, **{linked} связей**, **{iterations.Count} спринтов**. Создано в этом запуске: **{created}**.\n\n[Открыть тестовые карточки](https://github.com/{repository}/issues?q=is%3Aissue%20label%3A{Uri.EscapeDataString(seedLabel)})\n\nСборка .NET 8 и шаблон включены в артефакт `demo-generator`. Повторный запуск сохраняет изменения карточек и не создаёт дубликаты.\n";
Console.WriteLine(summary);
var summaryPath = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
if (summaryPath is not null) await File.AppendAllTextAsync(summaryPath, summary);
