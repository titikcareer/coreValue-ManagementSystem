namespace main.Services;
public record PicRecord(string Id, string Name, string Department);
public enum CoreValueTheme
{
    ResponsibleToTask,
    AgileAndAdaptive,
    Collaboration,
    ExcellentAndProfessional,
}
public static class CoreValueThemeExtensions
{
    /// Returns the user-facing label shown in selectors and banners
    public static string DisplayName(this CoreValueTheme theme) => theme switch
    {
        CoreValueTheme.ResponsibleToTask => "Responsible to Task",
        CoreValueTheme.AgileAndAdaptive => "Agile & Adaptive",
        CoreValueTheme.Collaboration => "Collaboration",
        CoreValueTheme.ExcellentAndProfessional => "Excellent & Professional",
        _ => theme.ToString(),
    };

    /// Maps to wwwroot/images/coreValue-{slug}.png
    public static string ImageSlug(this CoreValueTheme theme) => theme switch
    {
        CoreValueTheme.ResponsibleToTask => "responsibleToTask",
        CoreValueTheme.AgileAndAdaptive => "agileAdaptive",
        CoreValueTheme.Collaboration => "collaboration",
        CoreValueTheme.ExcellentAndProfessional => "excellentProfessional",
        _ => "collaboration",
    };
}
public class AgentStore
{
    private readonly object _lock = new();
    private readonly Dictionary<CoreValueTheme, List<PicRecord>> _agentsByTheme;
    private CoreValueTheme _currentTheme = CoreValueTheme.Collaboration;

    public event Action? Changed;
    public AgentStore()
    {
        _agentsByTheme = new Dictionary<CoreValueTheme, List<PicRecord>>
        {
            [CoreValueTheme.Collaboration] = new()
            {
                new("011915", "Fajri Kurniawan", "GA, CSR, KM, SECURITY"),
                new("011916", "Suarsono", "GA, CSR, KM, SECURITY"),
                new("011917", "M. Amirudin", "GA, CSR, KM, SECURITY"),
                new("011918", "Sri Haryanto", "Digital Transformation"),
            },
            [CoreValueTheme.ResponsibleToTask] = new(),
            [CoreValueTheme.AgileAndAdaptive] = new(),
            [CoreValueTheme.ExcellentAndProfessional] = new(),
        };
    }
    public CoreValueTheme CurrentTheme
    {
        get { lock (_lock) { return _currentTheme; } }
    }
    public void SetCurrentTheme(CoreValueTheme theme)
    {
        lock (_lock)
        {
            if (!_agentsByTheme.ContainsKey(theme))
            {
                _agentsByTheme[theme] = new List<PicRecord>();
            }
            _currentTheme = theme;
        }
        Changed?.Invoke();
    }
    public IReadOnlyList<PicRecord> GetCurrentAgents()
    {
        lock (_lock)
        {
            return _agentsByTheme[_currentTheme].ToList();
        }
    }
    public bool TryAdd(PicRecord agent)
    {
        lock (_lock)
        {
            var list = _agentsByTheme[_currentTheme];
            if (list.Any(a => a.Id.Equals(agent.Id, StringComparison.OrdinalIgnoreCase)))
            {
                // duplicate NIM within this themes roster
                return false;
            }

            list.Add(agent);
        }

        Changed?.Invoke();
        return true;
    }
    public bool TryUpdate(string originalId, PicRecord updated)
    {
        lock (_lock)
        {
            var list = _agentsByTheme[_currentTheme];
            var index = list.FindIndex(a => a.Id.Equals(originalId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return false;
            }

            // Guard against renaming a NIM into collision with a different existing agent
            if (!updated.Id.Equals(originalId, StringComparison.OrdinalIgnoreCase)
                && list.Any(a => a.Id.Equals(updated.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            list[index] = updated;
        }

        Changed?.Invoke();
        return true;
    }
    public bool TryDelete(string id)
    {
        lock (_lock)
        {
            var list = _agentsByTheme[_currentTheme];
            if (list.RemoveAll(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) == 0)
            {
                return false;
            }
        }

        Changed?.Invoke();
        return true;
    }
}
