using SwiftRide.MatchingService.Application.DTOs;

namespace SwiftRide.MatchingService.Application.Interfaces;

public interface IMatchingService
{
    Task<MatchResponse> MatchAsync(MatchRequest request, CancellationToken cancellationToken);
}
