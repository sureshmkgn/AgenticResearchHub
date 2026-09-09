using System.Reflection;
using AgenticResearchHub.Core.Prompts;
using Microsoft.Extensions.Logging;

namespace AgenticResearchHub.Agents.Prompts;

/// <summary>
/// Manages embedded fallback prompt templates and live disk hot-reloading with FileSystemWatcher.
/// </summary>
public sealed class PromptFileWatcher : IDisposable
{
    private readonly IPromptRegistry _registry;
    private readonly ILogger<PromptFileWatcher> _logger;
    private readonly List<FileSystemWatcher> _watchers = new();
    private bool _disposed;

    public PromptFileWatcher(IPromptRegistry registry, ILogger<PromptFileWatcher> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Loads all embedded .prompt.md templates compiled into the Agents assembly.
    /// </summary>
    public int LoadEmbeddedTemplates()
    {
        var assembly = typeof(PromptFileWatcher).Assembly;
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".prompt.md", StringComparison.OrdinalIgnoreCase))
            .ToList();

        int count = 0;
        foreach (var resName in resourceNames)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resName);
                if (stream == null) continue;

                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                var fallbackName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(resName));
                
                var template = PromptMarkdownParser.Parse(content, fallbackName);
                _registry.Register(template);
                _logger.LogInformation("Loaded embedded prompt template '{Name}' v{Version}", template.Name, template.Version);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load embedded prompt resource '{ResourceName}'", resName);
            }
        }

        return count;
    }

    /// <summary>
    /// Scans a directory for .prompt.md files and watches for changes to enable hot-reloading.
    /// </summary>
    public int WatchDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogDebug("Prompt directory '{DirectoryPath}' does not exist, skipping file watcher.", directoryPath);
            return 0;
        }

        int loadedCount = LoadDirectoryFiles(directoryPath);

        try
        {
            var watcher = new FileSystemWatcher(directoryPath)
            {
                Filter = "*.prompt.md",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            watcher.Changed += OnPromptFileChanged;
            watcher.Created += OnPromptFileChanged;
            watcher.Renamed += OnPromptFileRenamed;
            watcher.EnableRaisingEvents = true;

            _watchers.Add(watcher);
            _logger.LogInformation("Started hot-reload file watcher on '{DirectoryPath}' for *.prompt.md", directoryPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize FileSystemWatcher on '{DirectoryPath}'", directoryPath);
        }

        return loadedCount;
    }

    private int LoadDirectoryFiles(string directoryPath)
    {
        int count = 0;
        var files = Directory.GetFiles(directoryPath, "*.prompt.md", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (TryLoadPromptFile(file))
            {
                count++;
            }
        }
        return count;
    }

    private bool TryLoadPromptFile(string filePath)
    {
        try
        {
            // Allow file write completion before reading
            Thread.Sleep(50);
            var content = File.ReadAllText(filePath);
            var fallbackName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(filePath));
            var template = PromptMarkdownParser.Parse(content, fallbackName);
            
            _registry.Register(template);
            _logger.LogInformation("Hot-reloaded prompt template '{Name}' v{Version} from '{Path}'", template.Name, template.Version, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading prompt file from '{Path}'", filePath);
            return false;
        }
    }

    private void OnPromptFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("Detected change in prompt file '{FullPath}' ({ChangeType})", e.FullPath, e.ChangeType);
        TryLoadPromptFile(e.FullPath);
    }

    private void OnPromptFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogInformation("Prompt file renamed from '{OldFullPath}' to '{FullPath}'", e.OldFullPath, e.FullPath);
        TryLoadPromptFile(e.FullPath);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
    }
}
