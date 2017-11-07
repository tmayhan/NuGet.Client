// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging
{
    public class PackageExtractionResult
    {
        public bool PackageExisted { get; }

        public long PackageSize { get; }

        public TimeSpan PackageExtractionDuration { get; }

        public PackageExtractionResult(bool packageExisted, long packageSize, TimeSpan packageExtractionDuration)
        {
            PackageExisted = packageExisted;
            PackageSize = packageSize;
            PackageExtractionDuration = packageExtractionDuration;
        }
    }
}
