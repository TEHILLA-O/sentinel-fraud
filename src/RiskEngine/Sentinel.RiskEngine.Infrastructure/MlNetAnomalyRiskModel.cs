using Microsoft.ML;
using Microsoft.ML.Data;
using Sentinel.RiskEngine.Domain.Rules;

namespace Sentinel.RiskEngine.Infrastructure;

public sealed class MlNetAnomalyRiskModel : IRiskModel
{
    public const string Version = "mlnet-anomaly-1.0.0";

    private readonly object _gate = new();
    private readonly MLContext _ml = new(seed: 42);
    private ITransformer? _model;

    public Task<ModelRiskResult> ScoreAsync(RiskContext context, CancellationToken cancellationToken)
    {
        EnsureModel();
        var engine = _ml.Model.CreatePredictionEngine<RiskFeatures, RiskPrediction>(_model!);
        var prediction = engine.Predict(new RiskFeatures
        {
            Amount = (float)context.Transaction.Amount.Amount,
            Velocity5m = context.Velocity.TransactionsLast5Minutes,
            DistinctCountries = context.Velocity.DistinctCountriesLast30Minutes,
            CardPresent = context.Transaction.CardPresent ? 1f : 0f,
            AccountAgeDays = (float)(context.EvaluatedAt - context.Profile.AccountOpenedAt).TotalDays
        });

        var score = (int)Math.Clamp(Math.Round((1 - prediction.Score) * 100), 0, 100);
        return Task.FromResult(new ModelRiskResult
        {
            Score = score,
            Confidence = 0.55,
            ModelVersion = Version,
            Reason = "Optional ML.NET anomaly score trained on synthetic demo data. Not used as a blocking authority."
        });
    }

    private void EnsureModel()
    {
        if (_model is not null)
        {
            return;
        }

        lock (_gate)
        {
            if (_model is not null)
            {
                return;
            }

            var data = Enumerable.Range(0, 200).Select(i => new RiskFeatures
            {
                Amount = 40 + i % 80,
                Velocity5m = 1 + i % 3,
                DistinctCountries = 1,
                CardPresent = i % 4 == 0 ? 0f : 1f,
                AccountAgeDays = 200 + i
            }).ToList();

            var training = _ml.Data.LoadFromEnumerable(data);
            var pipeline = _ml.Transforms.Concatenate(
                    "Features",
                    nameof(RiskFeatures.Amount),
                    nameof(RiskFeatures.Velocity5m),
                    nameof(RiskFeatures.DistinctCountries),
                    nameof(RiskFeatures.CardPresent),
                    nameof(RiskFeatures.AccountAgeDays))
                .Append(_ml.AnomalyDetection.Trainers.RandomizedPca(
                    featureColumnName: "Features",
                    rank: 3));
            _model = pipeline.Fit(training);
        }
    }

    private sealed class RiskFeatures
    {
        public float Amount { get; set; }

        public float Velocity5m { get; set; }

        public float DistinctCountries { get; set; }

        public float CardPresent { get; set; }

        public float AccountAgeDays { get; set; }
    }

    private sealed class RiskPrediction
    {
        [ColumnName("Score")]
        public float Score { get; set; }

        [ColumnName("PredictedLabel")]
        public bool PredictedLabel { get; set; }
    }
}
