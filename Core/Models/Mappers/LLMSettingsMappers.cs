using graphnotelm.Core.Models.DTOs;
using Riok.Mapperly.Abstractions;

namespace graphnotelm.Core.Models.Mappers
{
    [Mapper]
    public static partial class LLMSettingsMapper
    {
        [UserMapping(Default = true)]
        public static LLMSettingsResponse ToLLMSettingsResponse(this LLMProviderConfig config)
        {
            var response = MapToResponse(config);
            response.HasApiKey = !string.IsNullOrEmpty(config.ApiKey);
            return response;
        }

        [UserMapping(Default = false)]
        [MapperIgnoreSource(nameof(LLMProviderConfig.ApiKey))]
        [MapperIgnoreTarget(nameof(LLMSettingsResponse.HasApiKey))]
        private static partial LLMSettingsResponse MapToResponse(LLMProviderConfig config);

        // Patch semantics: only provided values overwrite the current configuration.
        public static LLMProviderConfig MergeInto(this LLMSettingsRequest request, LLMProviderConfig current)
            => new()
            {
                Provider = request.Provider,
                ApiKey = !string.IsNullOrWhiteSpace(request.ApiKey) ? request.ApiKey! : current.ApiKey,
                Model = request.Model ?? current.Model,
                Endpoint = request.Endpoint ?? current.Endpoint
            };
    }
}
