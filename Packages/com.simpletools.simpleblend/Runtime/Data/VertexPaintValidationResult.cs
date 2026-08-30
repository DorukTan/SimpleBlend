using System;

namespace SimpleBlend
{
    [Serializable]
    public struct VertexPaintValidationResult
    {
        private readonly VertexPaintValidationFailure _failure;
        private readonly string _detail;

        private VertexPaintValidationResult(VertexPaintValidationFailure failure, string detail)
        {
            _failure = failure;
            _detail = detail;
        }

        public bool IsValid => _failure == VertexPaintValidationFailure.None;

        public VertexPaintValidationFailure Failure => _failure;

        public string Message => string.IsNullOrEmpty(_detail) ? VertexPaintText.For(_failure) : _detail;

        public static VertexPaintValidationResult Valid()
        {
            return new VertexPaintValidationResult(VertexPaintValidationFailure.None, string.Empty);
        }

        public static VertexPaintValidationResult Invalid(VertexPaintValidationFailure failure, string detail = null)
        {
            if (failure == VertexPaintValidationFailure.None)
            {
                throw new ArgumentException("Use Valid() for a successful validation result.", nameof(failure));
            }

            return new VertexPaintValidationResult(failure, detail);
        }
    }
}
