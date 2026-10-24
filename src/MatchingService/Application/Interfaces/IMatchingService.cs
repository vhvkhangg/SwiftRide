using SwiftRide.MatchingService.Application.DTOs;
using SwiftRide.Contracts.Matching;

namespace SwiftRide.MatchingService.Application.Interfaces;

public interface IMatchingService
{
    Task<MatchResponse> MatchAsync(MatchRequest request, CancellationToken cancellationToken);
    Task<MatchedDriver?> FindNextDriverAsync(FindDriverForTripV1 request, CancellationToken cancellationToken);
}
