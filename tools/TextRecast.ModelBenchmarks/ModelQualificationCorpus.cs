using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TextRecast.Core.Formatting;

namespace TextRecast.ModelBenchmarks;

public enum ModelQualificationSplit
{
    Development,
    Validation,
    Holdout
}

public enum ModelQualificationCorpusScope
{
    PromptDevelopment,
    PromptValidation,
    FinalQualification
}

public enum ModelQualificationLengthIntent
{
    Unconstrained,
    MoreConcise,
    MoreExplicit,
    Summarized
}

public sealed record ModelQualificationExpectation(
    IReadOnlyList<string> RequiredTerms,
    IReadOnlyList<string> ForbiddenTerms,
    IReadOnlyList<string> LanguageMarkers,
    IReadOnlyList<string> SemanticRequirements,
    ModelQualificationLengthIntent LengthIntent);

public sealed record ModelQualificationCase(
    string Id,
    string Category,
    ModelQualificationSplit Split,
    string Language,
    FormatTextRequest Request,
    IReadOnlyList<string> RiskTags,
    ModelQualificationExpectation Expectation);

public static class ModelQualificationTaskGroups
{
    public const string Improve = "improve";
    public const string Shorten = "shorten";
    public const string Lengthen = "lengthen";
    public const string Summarize = "summarize";
    public const string ToneProfessional = "tone-professional";
    public const string ToneCasual = "tone-casual";
    public const string ToneFriendly = "tone-friendly";
    public const string ToneFormal = "tone-formal";
    public const string ToneDirect = "tone-direct";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
    [
        Improve,
        Shorten,
        Lengthen,
        Summarize,
        ToneProfessional,
        ToneCasual,
        ToneFriendly,
        ToneFormal,
        ToneDirect
    ]);
}

public static class ModelQualificationCorpus
{
    public const string Version = "english-v2-2026-08-01";

    public static IReadOnlyList<ModelQualificationCase> English { get; } =
        Array.AsReadOnly(EnglishQualificationCases.Create());

    public static IReadOnlyList<ModelQualificationCase> DeferredMultilingual { get; } =
        Array.AsReadOnly(CreateDeferredMultilingualCases());

    public static IReadOnlyList<ModelQualificationCase> All { get; } =
        Array.AsReadOnly(English.Concat(DeferredMultilingual).ToArray());

    public static string EnglishFingerprint { get; } = ComputeFingerprint(English);

    public static IReadOnlyList<ModelQualificationCase> GetCases(
        ModelQualificationCorpusScope scope)
    {
        return scope switch
        {
            ModelQualificationCorpusScope.PromptDevelopment => Filter(ModelQualificationSplit.Development),
            ModelQualificationCorpusScope.PromptValidation => Filter(ModelQualificationSplit.Validation),
            ModelQualificationCorpusScope.FinalQualification => English,
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };
    }

    private static ModelQualificationCase[] Filter(ModelQualificationSplit split)
    {
        return English.Where(testCase => testCase.Split == split).ToArray();
    }

    private static string ComputeFingerprint(IReadOnlyList<ModelQualificationCase> cases)
    {
        var canonicalJson = JsonSerializer.Serialize(cases);
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
    }

    private static ModelQualificationCase[] CreateDeferredMultilingualCases()
    {
        return
        [
            CreateDeferred(
                "improve-hi-deferred",
                "hi",
                "कृपया रिपोर्ट शुक्रवार से पहले भेज दें क्योंकि समीक्षा सोमवार को शुरू होगी",
                FormatOperation.Improve,
                required: ["रिपोर्ट", "शुक्रवार", "सोमवार"],
                languageMarkers: ["रिपोर्ट", "शुक्रवार", "सोमवार"]),
            CreateDeferred(
                "shorten-es-deferred",
                "es",
                "Por favor, envía el informe financiero actualizado antes del viernes para que el equipo pueda comenzar la revisión a tiempo.",
                FormatOperation.Shorten,
                required: ["informe", "viernes"],
                languageMarkers: ["informe", "viernes"],
                lengthIntent: ModelQualificationLengthIntent.MoreConcise),
            CreateDeferredTone(
                "tone-professional-fr-deferred",
                "fr",
                "Votre équipe a encore oublié le rapport, alors envoyez-le avant lundi.",
                ToneStyle.Professional,
                required: ["rapport", "lundi"],
                languageMarkers: ["rapport", "lundi"]),
            CreateDeferredTone(
                "tone-formal-de-deferred",
                "de",
                "ich kann heute nicht kommen, schick mir bitte die notizen",
                ToneStyle.Formal,
                required: ["heute", "Notizen"],
                languageMarkers: ["heute", "Notizen"]),
            CreateDeferred(
                "summarize-ja-deferred",
                "ja",
                "新しいルーターは今朝到着しました。田中さんが設定を復元し、十二台の端末が接続できることを確認しました。古いルーターは明日返送します。",
                FormatOperation.Summarize,
                required: ["ルーター", "十二"],
                languageMarkers: ["ルーター", "十二"],
                lengthIntent: ModelQualificationLengthIntent.Summarized)
        ];
    }

    private static ModelQualificationCase CreateDeferred(
        string id,
        string language,
        string text,
        FormatOperation operation,
        IReadOnlyList<string> required,
        IReadOnlyList<string> languageMarkers,
        ModelQualificationLengthIntent lengthIntent = ModelQualificationLengthIntent.Unconstrained)
    {
        return new ModelQualificationCase(
            id,
            "multilingual-deferred",
            ModelQualificationSplit.Holdout,
            language,
            new FormatTextRequest(text, operation),
            ["language-preservation"],
            new ModelQualificationExpectation(
                required,
                [],
                languageMarkers,
                ["Preserve the complete source meaning in the source language."],
                lengthIntent));
    }

    private static ModelQualificationCase CreateDeferredTone(
        string id,
        string language,
        string text,
        ToneStyle tone,
        IReadOnlyList<string> required,
        IReadOnlyList<string> languageMarkers)
    {
        return new ModelQualificationCase(
            id,
            "multilingual-deferred",
            ModelQualificationSplit.Holdout,
            language,
            new FormatTextRequest(text, FormatOperation.ChangeTone, tone),
            ["language-preservation"],
            new ModelQualificationExpectation(
                required,
                [],
                languageMarkers,
                ["Change only the tone and preserve the complete source meaning in the source language."],
                ModelQualificationLengthIntent.Unconstrained));
    }
}
