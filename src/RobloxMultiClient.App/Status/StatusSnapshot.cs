using RobloxMultiClient.Core.Clients;
using RobloxMultiClient.Core.Errors;
using RobloxMultiClient.Core.Singleton;

namespace RobloxMultiClient.App.Status;

/// <summary>Everything the window shows at one point in time.</summary>
public sealed record StatusSnapshot(
    bool Enabled,
    bool Ready,
    IReadOnlyList<SingletonClaim> Claims,
    IReadOnlyList<RobloxClientProcess> Clients,
    string LogDirectory,
    string StatusMessage);

/// <summary>The status view. Implemented by the form, which holds no state of its own.</summary>
public interface IStatusView
{
    void Render(StatusSnapshot snapshot);

    void ShowError(AppError error);
}
