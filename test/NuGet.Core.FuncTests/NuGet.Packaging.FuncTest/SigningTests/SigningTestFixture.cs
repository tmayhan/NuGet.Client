// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;
using Test.Utility.Signing;

namespace NuGet.Packaging.FuncTest
{
    /// <summary>
    /// Used to bootstrap functional tests for signing.
    /// </summary>
    public class SigningTestFixture : IDisposable
    {
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private TrustedTestCert<TestCertificate> _trustedTestCertExpired;
        private TrustedTestCert<TestCertificate> _trustedTestCertNotYetValid;
        private TrustedTestCert<X509Certificate2> _trustedTimestampRoot;
        private IReadOnlyList<TrustedTestCert<TestCertificate>> _trustedTestCertificateWithReissuedCertificate;
        private IList<ISignatureVerificationProvider> _trustProviders;
        private SigningSpecifications _signingSpecifications;
        private Lazy<SigningTestServer> _testServer;
        private Lazy<CertificateAuthority> _defaultTrustedCertificateAuthority;
        private Lazy<TimestampService> _defaultTrustedTimestampService;
        private readonly DisposableList _responders;

        public ISigningTestServer TestServer => _testServer.Value;
        public CertificateAuthority DefaultTrustedCertificateAuthority => _defaultTrustedCertificateAuthority.Value;
        public TimestampService DefaultTrustedTimestampService => _defaultTrustedTimestampService.Value;

        public SigningTestFixture()
        {
            _testServer = new Lazy<SigningTestServer>(SigningTestServer.Create);
            _defaultTrustedCertificateAuthority = new Lazy<CertificateAuthority>(CreateDefaultTrustedCertificateAuthority);
            _defaultTrustedTimestampService = new Lazy<TimestampService>(CreateDefaultTrustedTimestampService);
            _responders = new DisposableList();
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificate
        {
            get
            {
                if (_trustedTestCert == null)
                {
                    _trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate();
                }

                return _trustedTestCert;
            }
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificateExpired
        {
            get
            {
                if (_trustedTestCertExpired == null)
                {
                    _trustedTestCertExpired = SigningTestUtility.GenerateTrustedTestCertificateExpired();
                }

                return _trustedTestCertExpired;
            }
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificateNotYetValid
        {
            get
            {
                if (_trustedTestCertNotYetValid == null)
                {
                    _trustedTestCertNotYetValid = SigningTestUtility.GenerateTrustedTestCertificateNotYetValid();
                }

                return _trustedTestCertNotYetValid;
            }
        }

        public IReadOnlyList<TrustedTestCert<TestCertificate>> TrustedTestCertificateWithReissuedCertificate
        {
            get
            {
                if (_trustedTestCertificateWithReissuedCertificate == null)
                {
                    var keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);
                    var certificateName = TestCertificate.GenerateCertificateName();
                    var certificate1 = SigningTestUtility.GenerateCertificate(certificateName, keyPair);
                    var certificate2 = SigningTestUtility.GenerateCertificate(certificateName, keyPair);

                    var testCertificate1 = new TestCertificate() { Cert = certificate1 }.WithTrust(StoreName.Root, StoreLocation.LocalMachine);
                    var testCertificate2 = new TestCertificate() { Cert = certificate2 }.WithTrust(StoreName.Root, StoreLocation.LocalMachine);

                    _trustedTestCertificateWithReissuedCertificate = new[]
                    {
                        testCertificate1,
                        testCertificate2
                    };
                }

                return _trustedTestCertificateWithReissuedCertificate;
            }
        }

        public IList<ISignatureVerificationProvider> TrustProviders
        {
            get
            {
                if (_trustProviders == null)
                {
                    _trustProviders = new List<ISignatureVerificationProvider>()
                    {
                        new SignatureTrustAndValidityVerificationProvider(),
                        new IntegrityVerificationProvider()
                    };
                }

                return _trustProviders;
            }
        }

        public SigningSpecifications SigningSpecifications
        {
            get
            {
                if (_signingSpecifications == null)
                {
                    _signingSpecifications = SigningSpecifications.V1;
                }

                return _signingSpecifications;
            }
        }

        public void Dispose()
        {
            _trustedTestCert?.Dispose();
            _trustedTestCertExpired?.Dispose();
            _trustedTestCertNotYetValid?.Dispose();
            _trustedTimestampRoot?.Dispose();
            _responders.Dispose();

            if (_trustedTestCertificateWithReissuedCertificate != null)
            {
                foreach (var certificate in _trustedTestCertificateWithReissuedCertificate)
                {
                    certificate.Dispose();
                }
            }

            if (_testServer.IsValueCreated)
            {
                _testServer.Value.Dispose();
            }
        }

        private CertificateAuthority CreateDefaultTrustedCertificateAuthority()
        {
            var rootCa = CertificateAuthority.Create(TestServer.Url);
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();
            var rootCertificate = new X509Certificate2(rootCa.Certificate.GetEncoded());

            _trustedTimestampRoot = new TrustedTestCert<X509Certificate2>(
                rootCertificate,
                _ => _,
                StoreName.Root,
                StoreLocation.LocalMachine);

            var ca = intermediateCa;

            while (ca != null)
            {
                _responders.Add(TestServer.RegisterResponder(ca));
                _responders.Add(TestServer.RegisterResponder(ca.OcspResponder));

                ca = ca.Parent;
            }

            return intermediateCa;
        }

        private TimestampService CreateDefaultTrustedTimestampService()
        {
            var timestampService = TimestampService.Create(DefaultTrustedCertificateAuthority);

            _responders.Add(TestServer.RegisterResponder(timestampService));

            return timestampService;
        }
    }
}