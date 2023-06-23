using System;
using USISampleCode.USIServiceReference;

namespace USISampleCode
{
    internal static class RequestFactory
    {
        //  OrganisationCode must be changed to "VA1802" for service providers. 
        private const string OrganisationCode = "VA1803";
        private static readonly Random Random = new Random();

        public static UpdateUSIContactDetailsRequest CreateUpdateContactDetailsRequest(string orgCode, string usi)
        {
            return new UpdateUSIContactDetailsRequest
            {
                UpdateUSIContactDetails = new UpdateUSIContactDetailsType
                {
                    ContactDetailsUpdate = new ContactDetailsUpdateType
                    {
                        CountryOfResidenceCode = "1101",
                        EmailAddress = "jane@test.com",
                        Item = new NationalAddressType
                        {
                            Address1 = "14 Close Cct",
                            PostCode = "2020",
                            State = StateListType.NSW,
                            SuburbTownCity = "Mascot"
                        },
                        Phone = new PhoneType { Mobile = "0412341234" }
                    },
                    OrgCode = orgCode,
                    UserReference = "CalledBySample",
                    USI = usi,
                },
            };
        }

        public static GetNonDvsDocumentTypesRequest CreateGetNonDvsDocumentRequest(string orgCode)
        {
            return new GetNonDvsDocumentTypesRequest
            {
                GetNonDvsDocumentTypes = new GetNonDvsDocumentTypesType { OrgCode = orgCode },
            };
        }

        public static CreateUSIRequest CreateUsiRequest(ApplicationType application)
        {
            var requestType = new CreateUSIType
            {
                OrgCode = OrganisationCode,
                RequestId = Random.Next(100000000, 999999999).ToString(),
                Application = application,
            };

            return new CreateUSIRequest(requestType);
        }

        public static BulkUploadRequest CreateBulkUploadRequest(ApplicationType[] applications)
        {
            var bulkUploadType = new BulkUploadType
            {
                OrgCode = OrganisationCode,
                RequestId = Random.Next(100000000, 999999999).ToString(),
                NoOfApplications = applications.Length,
                Applications = applications,
            };

            return new BulkUploadRequest(bulkUploadType);
        }

        public static BulkVerifyUSIRequest CreateBulkVerifyRequest(VerificationType[] verifications)
        {
            var bulkVerifyUsiType = new BulkVerifyUSIType
            {
                OrgCode = OrganisationCode,
                NoOfVerifications = verifications.Length,
                Verifications = verifications,
            };

            return new BulkVerifyUSIRequest(bulkVerifyUsiType);
        }

        public static ApplicationType CreateApplication(
            string firstName,
            string lastName,
            DateTime dateOfBirth,
            PersonalDetailsTypeGender gender,
            string emailAddress,
            string phoneNumber,
            string address,
            string postCode,
            StateListType state,
            string suburbTownCity,
            DVSDocumentType document)
        {
            return new ApplicationType
            {
                ContactDetails = new ContactDetailsType
                {
                    CountryOfResidenceCode = "1101",
                    EmailAddress = emailAddress,
                    Item = new NationalAddressType
                    {
                        Address1 = address,
                        PostCode = postCode,
                        State = state,
                        SuburbTownCity = suburbTownCity
                    },
                    Phone = new PhoneType { Home = phoneNumber },
                },
                DVSCheckRequired = true,
                DVSDocument = document,
                //Items = new [] { document },
                PersonalDetails = new PersonalDetailsType
                {
                    CountryOfBirthCode = "1101",
                    DateOfBirth = dateOfBirth,
                    Gender = gender,
                    Items = new[] { firstName, lastName },
                    ItemsElementName = new[] { ItemsChoiceType.FirstName, ItemsChoiceType.FamilyName },
                    TownCityOfBirth = suburbTownCity,
                },
                UserReference = "CalledBySample",
                ApplicationId = Random.Next(100000, 999999).ToString(),
            };
        }

        public static VerificationType CreateVerification(
            int recordId,
            string usi,
            string firstName,
            string lastName,
            DateTime dateOfBirth)
        {
            var verificationType = new VerificationType
            {
                RecordId = recordId,
                DateOfBirth = dateOfBirth,
                ItemsElementName = new[] { ItemsChoiceType1.FirstName, ItemsChoiceType1.FamilyName },
                Items = new[] { firstName, lastName },
                USI = usi,
            };

            return verificationType;
        }

        public static DVSDocumentType BirthCert()
        {
            return new BirthCertificateDocumentType
            {
                CertificateNumber = "1111111",
                DatePrinted = DateTime.Today,
                DatePrintedSpecified = true,
                RegistrationDate = DateTime.Today,
                RegistrationDateSpecified = true,
                RegistrationNumber = "1111111", // Ensures the mock DVS service will pass the verification.
                RegistrationState = StateListType.NSW,
                //RegistrationYear = DateTime.Today.Year.ToString()
            };
        }

        public static DVSDocumentType Citizenship()
        {
            return new CitizenshipCertificateDocumentType
            {
                AcquisitionDate = new DateTime(2010, 01, 01),
                StockNumber = "ACC111111", // Ensures the mock DVS service will pass the verification.
            };
        }

        public static DVSDocumentType Descent()
        {
            return new CertificateOfRegistrationByDescentDocumentType
            {
                AcquisitionDate = new DateTime(2013, 01, 01), // Ensures the mock DVS service will pass the verification.     
            };
        }

        public static DVSDocumentType Visa()
        {
            return new VisaDocumentType
            {
                PassportNumber = "111111", // Ensures the mock DVS service will pass the verification.
            };
        }

        public static DVSDocumentType Passport()
        {
            return new PassportDocumentType
            {
                DocumentNumber = "X1111111", // Ensures the mock DVS service will pass the verification.
            };
        }

        public static DVSDocumentType ImmiCard()
        {
            return new ImmiCardDocumentType
            {
                ImmiCardNumber = "ABC111111",
            };
        }

        public static DVSDocumentType Medicare(string name)
        {
            // the name on a medicare card can be split onto 4 lines. the maximum lengths of lines 1-4 are 27,25,23,21 characters respectively
            var names = SplitToLengths(name, new[] { 27, 25, 23, 21 });
            return new MedicareDocumentType
            {
                NameLine1 = names[0],
                NameLine2 = names[1],
                NameLine3 = names[2],
                NameLine4 = names[3],
                CardColour = MedicareDocumentTypeCardColour.Green,
                ExpiryDate = "2015-12",
                IndividualRefNumber = "3",
                MedicareCardNumber = "1111111111", // Ensures the mock DVS service will pass the verification.
            };
        }

        public static DVSDocumentType DriversLicence()
        {
            return new DriversLicenceDocumentType
            {
                LicenceNumber = "111111", // Ensures the mock DVS service will pass the verification.
                State = StateListType.ACT,
                CardNumber = "A001234567",
            };
        }

        private static string[] SplitToLengths(this string stringToSplit, int[] lengths)
        {
            var retSet = new string[lengths.Length];
            var remaining = stringToSplit;
            for (var ii = 0; ii < lengths.Length; ii++)
            {
                if (remaining.Length == 0) return retSet;
                var sectionLength = Math.Min(lengths[ii], remaining.Length);
                retSet[ii] = remaining.Substring(0, sectionLength);
                remaining = remaining.Substring(sectionLength);
            }

            return retSet;
        }
    }
}