// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using CERTENROLLLib;
using System;
using System.Security.Cryptography.X509Certificates;


namespace AutomationISE.Model
{
    /// <summary>
    /// A generic certificate class to create private (.PFX) certificates.
    /// It usese the CERTENROLLib library for core capabilities. https://msdn.microsoft.com/en-us/library/windows/desktop/aa374846(v=vs.85).aspx
    /// </summary>
    class Certificate
    {

        private CX509CertificateRequestCertificate objCertRequest = null;
        private CX509PrivateKey objPrivateKey = null;
        private CCspInformation objCSP = null;
        private CCspInformations objCSPs = null;
        private CX500DistinguishedName objDN = null;
        private CX509Enrollment objEnroll = null;
        private CObjectId objObjectId = null;
        private String stringResponse = null;

        /// <summary>
        /// Certificate constructor to intialize objects and values required to create a certificate
        /// </summary>
        public Certificate()
        {
            try
            {
                // Create objects required
                objCertRequest = new CX509CertificateRequestCertificate();
                objPrivateKey = new CX509PrivateKey();
                objCSP = new CCspInformation();
                objCSPs = new CCspInformations();
                objDN = new CX500DistinguishedName();
                objEnroll = new CX509Enrollment();
                objObjectId = new CObjectId();

                // Friendly name
                this.FriendlyName = "";

                // Set default values. Refer to https://msdn.microsoft.com/en-us/library/windows/desktop/aa374846(v=vs.85).aspx
                this.CryptographicProviderName = "Microsoft Enhanced Cryptographic Provider v1.0";
                this.KeySize = 2048;

                // Use key for encryption
                this.KeySpec = X509KeySpec.XCN_AT_KEYEXCHANGE;

                // The key can be used for decryption
                this.KeyUsage = X509PrivateKeyUsageFlags.XCN_NCRYPT_ALLOW_DECRYPT_FLAG;

                // Create for user and not machine
                this.MachineContext = false;

                // Default to expire in 1 year
                this.ExpirationLengthInDays = 365;

                // Let th private key be exported in plain text
                this.ExportPolicy = X509PrivateKeyExportFlags.XCN_NCRYPT_ALLOW_PLAINTEXT_EXPORT_FLAG;

                // This is intended for a computer
                this.EnrollmentContextMachine = X509CertificateEnrollmentContext.ContextUser;

                // Use a hasing algorithm
                this.ObjectIdGroupId = ObjectIdGroupId. XCN_CRYPT_HASH_ALG_OID_GROUP_ID;
                this.ObjectIdPublicKeyFlags = ObjectIdPublicKeyFlags.XCN_CRYPT_OID_INFO_PUBKEY_ANY;
                this.AlgorithmFlags = AlgorithmFlags.AlgorithmFlagsNone;

                // Use SHA-2 with 512 bits
                this.AlgorithmName = "SHA512";
                this.EncodingType = EncodingType.XCN_CRYPT_STRING_BASE64;

                // Allow untrusted certificate to be installed
                this.InstallResponseRestrictionFlags = InstallResponseRestrictionFlags.AllowUntrustedCertificate;

                // No password set
                this.Password = null;

                // Enable key to be exported, keep the machine set, and persist the key set
                // https://msdn.microsoft.com/en-us/library/system.security.cryptography.x509certificates.x509keystorageflags(v=vs.110).aspx
                this.ExportableFlags = X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet;

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Creates a certificate request with the given name.
        /// </summary>
        /// <param name="name"></param>
        public void CreateCertificateRequest(String name)
        {
            try
            {
                // Initialize cryptographic provider
                objCSP.InitializeFromName(this.CryptographicProviderName);
                objCSPs.Add(objCSP);

                // Set CX509PrivateKey values and create 
                objPrivateKey.CspInformations = objCSPs;
                objPrivateKey.Length = this.KeySize;
                objPrivateKey.KeySpec = this.KeySpec;
                objPrivateKey.KeyUsage = this.KeyUsage;
                objPrivateKey.MachineContext = this.MachineContext;
                objPrivateKey.ExportPolicy = this.ExportPolicy;
                objPrivateKey.Create();

                // Initalize CX509CertificateRequestCertificate.
                objCertRequest.InitializeFromPrivateKey(this.EnrollmentContextMachine, objPrivateKey, "");

                objObjectId.InitializeFromAlgorithmName(this.ObjectIdGroupId,
                        this.ObjectIdPublicKeyFlags,
                        this.AlgorithmFlags, this.AlgorithmName);

                // Add the name passed in
                objDN.Encode(
                    "CN=" + name,
                    X500NameFlags.XCN_CERT_NAME_STR_NONE
                );

                objCertRequest.Subject = objDN;
                objCertRequest.Issuer = objDN;
                objCertRequest.HashAlgorithm = objObjectId;

                objCertRequest.NotBefore = DateTime.Now;
                objCertRequest.NotAfter = DateTime.Now + (DateTime.Now.Date.AddDays(this.ExpirationLengthInDays) - DateTime.Now.Date);

                objEnroll.InitializeFromRequest(objCertRequest);
                objEnroll.CertificateFriendlyName = this.FriendlyName;
                stringResponse = objEnroll.CreateRequest(this.EncodingType);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Installs the certificate
        /// </summary>
        /// <returns></returns>
        public X509Certificate2 InstallCertficate()
        {
            CX509Enrollment objEnroll = new CX509Enrollment();

            try
            {
                // Install the certificate
                objEnroll.InitializeFromRequest(objCertRequest);
                objEnroll.InstallResponse(
                    this.InstallResponseRestrictionFlags,
                    stringResponse,
                    this.EncodingType,
                    this.Password
                );

                var base64encoded = objEnroll.CreatePFX(this.Password, PFXExportOptions.PFXExportChainWithRoot);

                return new System.Security.Cryptography.X509Certificates.X509Certificate2(
                    System.Convert.FromBase64String(base64encoded), this.Password,
                    this.ExportableFlags);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        // Below are various values that can be set determining the type of certificate you want to create.
        public int KeySize { get; set; }
        public X509KeySpec KeySpec { get; set; }
        public X509PrivateKeyUsageFlags KeyUsage { get; set; }
        public Boolean MachineContext { get; set; }

        public String CryptographicProviderName { get; set; }

        public int ExpirationLengthInDays { get; set; }

        public String FriendlyName { get; set; }

        public X509PrivateKeyExportFlags ExportPolicy { get; set; }

        public X509CertificateEnrollmentContext EnrollmentContextMachine { get; set; }

        public ObjectIdGroupId ObjectIdGroupId { get; set; }

        public ObjectIdPublicKeyFlags ObjectIdPublicKeyFlags { get; set; }

        public InstallResponseRestrictionFlags InstallResponseRestrictionFlags { get; set; }

        public X509KeyStorageFlags ExportableFlags { get; set; }

        public AlgorithmFlags AlgorithmFlags { get; set; }

        public String AlgorithmName { get; set; }

        public EncodingType EncodingType { get; set; }

        public String Password { get; set; }
    }
}
