using MongoDB.Bson;
using MongoDB.Driver;
using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Infrastructure.Repositories;

public static class MongoDbSeeder
{
    public static async Task SeedAsync(IMongoDatabase database)
    {
        var promoCodes = database.GetCollection<PromoCodeDocument>("promo_codes");
        if (await promoCodes.CountDocumentsAsync(FilterDefinition<PromoCodeDocument>.Empty) == 0)
        {
            await promoCodes.InsertOneAsync(new PromoCodeDocument
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Code = "SWIFTRIDE2026",
                DiscountRate = 0.1m,
                IsActive = true,
                ExpiredAt = DateTime.UtcNow.AddYears(1)
            });
        }

        var surgeRules = database.GetCollection<SurgeRuleDocument>("surge_rules");
        if (await surgeRules.CountDocumentsAsync(FilterDefinition<SurgeRuleDocument>.Empty) == 0)
        {
            await surgeRules.InsertOneAsync(new SurgeRuleDocument
            {
                Id = ObjectId.GenerateNewId().ToString(),
                StartHour = 17,
                EndHour = 19,
                Multiplier = 1.2m
            });
        }
    }
}
