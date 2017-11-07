// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging.Signing;

namespace NuGet.Packaging
{
    public class SignedPackageExtractionResult : PackageExtractionResult
    {
        public bool SignatureVerifyResult { get; }

        public IReadOnlyList<SignatureVerificationResult> VerifiedResults { get; }

        public TimeSpan SignatureVerifyDuration { get; }

        public SignedPackageExtractionResult(
            long packageSize,
            TimeSpan packageExtractionDuration,
            bool signatureVerifyResult,
            TimeSpan signatureVerifyDuration,
            IEnumerable<SignatureVerificationResult> verifiedResults) :
            base(false, packageSize, packageExtractionDuration)
        {
            SignatureVerifyResult = signatureVerifyResult;
            VerifiedResults = verifiedResults?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(verifiedResults));
            SignatureVerifyDuration = signatureVerifyDuration;
        }
    }
}
