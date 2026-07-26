using TextRecast.Core.Application.Results;

namespace TextRecast.Core.Abstractions;

public interface ISelectionCaptureService
{
    Task<SelectionCaptureResult> CaptureSelectedTextAsync(IntPtr targetWindow);
}
