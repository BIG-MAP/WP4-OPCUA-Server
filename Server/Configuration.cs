using Opc.Ua;

namespace BeltLightSensor;

public static class Configuration
{
    public static ApplicationConfiguration Default()
    {
        const string appName = "BeltLightSensor";
        const string appUri = $"urn:eu:big-map:wp4:{appName}";
        const string productUri = $"urn:localhost:{appName}";
        const string registrationEndpointUrl = "opc.tcp://localhost:4840";
        const string securityPolicyUri = "http://opcfoundation.org/UA/SecurityPolicy#None";
        const string nodeManagerFilePath = $"{appName}_.NodeSet.xml";
        const string traceConfigurationFilePath = $"{appName}.log";
        var domainNames = new[] { "localhost", "host.docker.internal" };
        var baseAddresses = new[]
        {
            $"opc.tcp://localhost:62540/UA/{appName}",
            $"https://localhost:62541/UA/{appName}"
        };

        var securityPolicies = new ServerSecurityPolicyCollection
        {
            new()
            {
                SecurityMode = MessageSecurityMode.None,
                SecurityPolicyUri = securityPolicyUri
            }
        };

        const string subjectName = "WP4";

        var certificate = CertificateFactory
            .CreateCertificate(appUri, appName, subjectName, domainNames)
            .CreateForRSA();

        var certificateIdentifier = new CertificateIdentifier(certificate);

        var applicationConfiguration = new ApplicationConfiguration
        {
            ApplicationName = appName,
            ApplicationUri = appUri,
            ApplicationType = ApplicationType.Server,
            ProductUri = productUri,
            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses = baseAddresses,
                SecurityPolicies = securityPolicies,
                DiagnosticsEnabled = true,
                RegistrationEndpoint = new EndpointDescription
                {
                    EndpointUrl = registrationEndpointUrl,
                    SecurityMode = MessageSecurityMode.None,
                    SecurityPolicyUri = securityPolicyUri
                },
                NodeManagerSaveFile = nodeManagerFilePath,
                ServerCapabilities = new StringCollection { "DA", "HDA" },
                UserTokenPolicies = new UserTokenPolicyCollection
                {
                    new() { TokenType = UserTokenType.Anonymous },
                    new() { TokenType = UserTokenType.UserName },
                    new() { TokenType = UserTokenType.Certificate }
                }
            },
            TraceConfiguration = new TraceConfiguration
            {
                OutputFilePath = traceConfigurationFilePath,
                DeleteOnLoad = true
            },
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = certificateIdentifier,
                AutoAcceptUntrustedCertificates = true,
                RejectedCertificateStore = new CertificateStoreIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "RejectedCertificates"
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar +
                                "TrustedCertificatesIssuer"
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar +
                                "TrustedCertificatesPeers"
                },
                TrustedUserCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar +
                                "TrustedCertificatesUser"
                },
                UserIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "UserCertificatesIssuer"
                },
                SendCertificateChain = true,
                AddAppCertToTrustedStore = false,
                MinimumCertificateKeySize = 2048,
                RejectSHA1SignedCertificates = true,
                RejectUnknownRevocationStatus = true
            },
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 120000,
                MaxStringLength = 1048576,
                MaxByteStringLength = 1048576,
                MaxArrayLength = 65535,
                MaxMessageSize = 4194304,
                MaxBufferSize = 65535,
                ChannelLifetime = 300000,
                SecurityTokenLifetime = 3600000
            }
        };

        return applicationConfiguration;
    }
}