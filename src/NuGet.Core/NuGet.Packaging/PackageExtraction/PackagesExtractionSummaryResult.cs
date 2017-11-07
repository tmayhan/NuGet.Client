// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Packaging
{
    public class PackagesExtractionSummaryResult
    {
        public double PackagesSizeInMB { get; }

        public int PackageCountTobeExtracted { get; }

        public int PackageCountTobeSignatureVerified { get; }

        public int PackageCountSuccessfullySignatureVerified { get; }

        public TimeSpan ExtractionAndSignatureVerificationDuration { get; }

        public TimeSpan PackageVerifyDelay { get; }


        public PackagesExtractionSummaryResult(List<PackageExtractionResult> results, TimeSpan duration)
        {
            if (results.Count != 0)
            {
                PackageCountTobeExtracted = results.Where(p => !p.PackageExisted).Count();
                PackageCountTobeSignatureVerified = results.Where(p => p is SignedPackageExtractionResult).Count();
                PackageCountSuccessfullySignatureVerified = results.Where(p => p is SignedPackageExtractionResult && ((p as SignedPackageExtractionResult).SignatureVerifyResult)).Count();
                PackagesSizeInMB = results.Where(p => !p.PackageExisted).Sum(p => ConvertToMB(p.PackageSize));
                PackageVerifyDelay = GetVerifyDelay(results);
            }

            ExtractionAndSignatureVerificationDuration = duration;
        }

        PackagesExtractionSummaryResult(
            int packageCountTobeExtracted,
            int packageCountTobeSignatureVerified,
            int packageCountSuccessfullySignatureVerified,
            double packagesSizeInMB,
            TimeSpan extractionAndSignatureVerificationDuration)
        {
            PackageCountTobeExtracted = PackageCountTobeExtracted;
            PackageCountTobeSignatureVerified = packageCountTobeSignatureVerified;
            PackageCountSuccessfullySignatureVerified = packageCountSuccessfullySignatureVerified;
            PackagesSizeInMB = packagesSizeInMB;
            ExtractionAndSignatureVerificationDuration = extractionAndSignatureVerificationDuration;
        }

        public static PackagesExtractionSummaryResult operator + (PackagesExtractionSummaryResult result1, PackagesExtractionSummaryResult result2)
        {
            return new PackagesExtractionSummaryResult(
                result1.PackageCountTobeExtracted + result2.PackageCountTobeExtracted,
                result1.PackageCountTobeSignatureVerified + result2.PackageCountTobeSignatureVerified,
                result1.PackageCountSuccessfullySignatureVerified + result2.PackageCountSuccessfullySignatureVerified,
                result1.PackagesSizeInMB + result2.PackagesSizeInMB,
                result1.ExtractionAndSignatureVerificationDuration + result2.ExtractionAndSignatureVerificationDuration);
        }

        private double ConvertToMB(long bytes)
        {
            return (bytes / 1024f) / 1024f;
        }

        private TimeSpan GetVerifyDelay(List<PackageExtractionResult> results)
        {
            var maxExtractedDurationWithVerify = results.Where(p => p is SignedPackageExtractionResult).Max(p => p.PackageExtractionDuration);
            var maxExtractedDurationWithoutVerify = results.Select(p => GetExtractedDurationWithoutVerify(p)).Max();

            return maxExtractedDurationWithVerify > maxExtractedDurationWithoutVerify ? maxExtractedDurationWithVerify - maxExtractedDurationWithoutVerify : new TimeSpan();
        }

        private TimeSpan GetExtractedDurationWithoutVerify(PackageExtractionResult result)
        {
            var signedResult = result as SignedPackageExtractionResult;

            if (signedResult == null)
            {
                return result.PackageExtractionDuration;
            }
            else
            {
                return signedResult.PackageExtractionDuration - signedResult.SignatureVerifyDuration;
            }
        }
    }
}
