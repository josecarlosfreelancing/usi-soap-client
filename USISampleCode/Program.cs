using System;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.Text;
using USISampleCode.USIServiceReference;

namespace USISampleCode
{
    public class Program
    {
        private static void Main(string[] args)
        {
            // Set callback handler to validate a server certificate.
            ServicePointManager.ServerCertificateValidationCallback += CustomSslCertificateValidation;
            string option = null;
            while (true)
            {
                if (string.IsNullOrWhiteSpace(option))
                {
                    Console.WriteLine("Usage:");
                    Console.WriteLine("{0} <option>", Assembly.GetExecutingAssembly().GetName().Name);
                    Console.WriteLine("  /c                - Calls CreateUSI and VerifyUSI");
                    Console.WriteLine("  /b                - Calls BulkUpload returning a ReceiptNumber");
                    Console.WriteLine("  /r ReceiptNumber  - Calls BulkUploadRetrieve using ReceiptNumber");
                    Console.WriteLine("  /v                - Calls BulkVerify");
                    Console.WriteLine("  /uc               - Calls Update Contact Details");
                    Console.WriteLine("  /g                - Calls GetNonDvsDocuments");
                    Console.WriteLine("  /e                - Exit");
                    Console.WriteLine("Enter your choice and press enter.");
                    option = Console.ReadLine();
                    continue;
                }

                option = option.Trim().ToUpper();
                string output;
                if (option == "/C")
                {
                    output = PerformCreateAndVerifyUsi();
                }
                else if (option == "/B")
                {
                    output = PerformBulkUpload();
                }
                else if (option == "/R")
                {
                    Console.WriteLine("Enter the receipt number:");
                    var receiptNumber = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(receiptNumber))
                    {
                        output = PerformBulkUploadRetrieve(receiptNumber);
                    }
                    else
                    {
                        option = null;
                        continue;
                    }
                }
                else if (option == "/V")
                {
                    output = PerformBulkVerify();
                }
                else if (option == "/UC")
                {
                    output = UpdateContactDetails();
                }
                else if (option == "/G")
                {
                    output = GetNonDvsDocumentTypes();
                }
                else if (option == "/E")
                {
                    break;
                }
                else
                {
                    option = null;
                    continue;
                }

                Console.WriteLine(output);
                option = null;
            }
        }

        private static string UpdateContactDetails()
        {
            IUSIService client = null;
            var sb = new StringBuilder();
            try
            {
                // Permission is given to USI 7WCT4QEFEQ for testing.
                Console.WriteLine("Please enter a USI to update (you must have permission):");
                var usi = Console.ReadLine();
                if (string.IsNullOrEmpty(usi))
                {
                    return "Could not read the USI";
                }

                usi = usi.Trim();
                if (usi.Length != 10)
                {
                    return "USI should be 10 characters long";
                }

                // create a request to get nonDvsDocumentTypes
                var request = RequestFactory.CreateUpdateContactDetailsRequest("VA1803", usi);

                // Open a channel to USI service.
                client = ServiceChannel.OpenWithM2M();

                // Make the USI service call.
                UpdateUSIContactDetailsResponse response;
                try
                {
                    response = client.UpdateUSIContactDetails(request);
                }
                catch (FaultException<ErrorInfo> ex)
                {
                    sb.AppendLine("Get Non Dvs Documents returned a FaultException");
                    sb.AppendLine($"Detail: {ex.Detail.Message}");
                    return sb.ToString();
                }

                if (response.UpdateUSIContactDetailsResponse1.Result == UpdateUSIContactDetailsResponseTypeResult.Failure)
                {
                    var message = " " + string.Join(". ", response.UpdateUSIContactDetailsResponse1.Errors.Select(e => e.Message));
                    Console.WriteLine($"Update contact details Failed. Reason: {message}");
                    return message;
                }

                return "Successfully updated contact details";
            }
            finally
            {
                if (client is ICommunicationObject communicationObject)
                {
                    communicationObject.Close();
                }

                ServiceChannel.Close();
            }
        }

        private static string GetNonDvsDocumentTypes()
        {
            IUSIService client = null;
            var sb = new StringBuilder();
            try
            {
                // create a request to get nonDvsDocumentTypes
                var request = RequestFactory.CreateGetNonDvsDocumentRequest("VA1803");

                // Open a channel to the USI service.
                client = ServiceChannel.OpenWithM2M();

                // Make the USI service call.
                GetNonDvsDocumentTypesResponse response;
                try
                {
                    response = client.GetNonDvsDocumentTypes(request);
                }
                catch (FaultException<ErrorInfo> ex)
                {
                    sb.Append("Get Non Dvs Documents returned a FaultException").AppendLine($"Detail: {ex.Detail.Message}");
                    return sb.ToString();
                }

                var responseStrings = response.GetNonDvsDocumentTypesResponse1.NonDvsDocumentTypes;
                Console.WriteLine("The following non dvs document types were returned;" + Environment.NewLine);
                foreach (var nonDvsDocumentTypeType in responseStrings)
                {
                    sb.AppendLine($"Id:{nonDvsDocumentTypeType.Id} Type:{nonDvsDocumentTypeType.DocumentType} Sort Order:{nonDvsDocumentTypeType.SortOrder}");
                }

                return sb.ToString();
            }
            finally
            {
                if (client is ICommunicationObject communicationObject)
                {
                    communicationObject.Close();
                }

                ServiceChannel.Close();
            }
        }

        private static bool CustomSslCertificateValidation(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors error)
        {
#if DEBUG
            // IMPORTANT - This should not be used in production code.
            // The purpose of this is to allow the use of a mismatching SSL certificate, 
            // which is not expected to be required in third party or production.
            // This setting may create a "Man in the Middle" vulnerability.
            return !error.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable);
#else
            //This is the safe version of the above code.
            return error == SslPolicyErrors.None;
#endif
        }

        private static string DecodeBulkUploadRetrievalResponse(ApplicationResponseType response)
        {
            string message;

            // Determine overall success or failure of the call.
            switch (response.Result)
            {
                case ApplicationResponseTypeResult.Success:
                    message = $"Application {response.ApplicationId} succeeded with USI {response.USI}.";
                    break;
                case ApplicationResponseTypeResult.MatchFound:
                    message = $"Application {response.ApplicationId} already exists.";
                    break;
                case ApplicationResponseTypeResult.Failure:
                    message = $"Application {response.ApplicationId} failed.";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            message += response.USI != null ? $" USI={response.USI}." : " USI is null.";
            message += $" IdentityDocumentVerified={response.IdentityDocumentVerified}.";

            // Get the detailed messages if the call failed.
            if (response.Errors != null && response.Errors.Length > 0)
            {
                message += " " + string.Join(". ", response.Errors.Select(e => e.Message));
            }

            return message;
        }

        private static string DecodeBulkVerifyResponse(VerificationResponseType response)
        {
            var message = $"RecordId={response.RecordId}{Environment.NewLine}USI={response.USI}{Environment.NewLine}USIStatus={response.USIStatus}{Environment.NewLine}";
            var allItems = response.Items.ToList();
            for (var i = 0; i < allItems.Count; i++)
            {
                message += $"{response.ItemsElementName[i]}={allItems[i]}{Environment.NewLine}";
            }

            message += $"DateOfBirth={response.DateOfBirth}{Environment.NewLine}";
            return message;
        }

        private static string PerformBulkUpload()
        {
            IUSIService client = null;
            var sb = new StringBuilder();
            try
            {
                // Create an array of applications.
                var request = RequestFactory.CreateBulkUploadRequest(new[]
                {
                    // 09xxxxxxxx is a valid but non-existent phone number. http://australia.gov.au/about-australia/our-country/telephone-country-and-area-codes
                    // usi.sample.code@gmail.com is a registered but unused email address. Gmail ignores a plus sign and anything following it.
                    RequestFactory.CreateApplication(
                        "Johnny",
                        "Smithy",
                        new DateTime(1980, 01, 02),
                        PersonalDetailsTypeGender.M,
                        "usi.sample.code+bulk11f@gmail.com",
                        "0900000004",
                        "5 Johnny Street",
                        "4013", StateListType.QLD,
                        "NORTHGATE",
                        RequestFactory.BirthCert()),
                    RequestFactory.CreateApplication(
                        "Lucy",
                        "Kockhe",
                        new DateTime(1985, 03, 02),
                        PersonalDetailsTypeGender.F,
                        "usi.sample.code+bulk31f@gmail.com",
                        "0900000004",
                        "5 Luce Street",
                        "4013",
                        StateListType.QLD,
                        "NORTHGATE",
                        RequestFactory.Citizenship()),
                    RequestFactory.CreateApplication(
                        "Nichloas",
                        "Koke",
                        new DateTime(1990, 07, 02),
                        PersonalDetailsTypeGender.M,
                        "usi.sample.code+bulk41f@gmail.com",
                        "0900000004",
                        "5 Nice Street",
                        "4013",
                        StateListType.QLD,
                        "NORTHGATE",
                        RequestFactory.Descent()),
                    RequestFactory.CreateApplication(
                        "Bobby",
                        "Lashley",
                        new DateTime(1977, 09, 02),
                        PersonalDetailsTypeGender.M,
                        "usi.sample.code+bulk101f@gmail.com",
                        "0900000004",
                        "5 France Street",
                        "4013",
                        StateListType.QLD,
                        "NORTHGATE",
                        RequestFactory.DriversLicence()),
                    RequestFactory.CreateApplication(
                        "Clooney",
                        "Amal",
                        new DateTime(1981, 02, 02),
                        PersonalDetailsTypeGender.F,
                        "usi.sample.code+bulk201f@gmail.com",
                        "0900000004",
                        "5 Liz Street",
                        "4013",
                        StateListType.QLD,
                        "NORTHGATE",
                        RequestFactory.Medicare("Lisa Smith7f")),
                    RequestFactory.CreateApplication(
                        "Greg",
                        "Clooney",
                        new DateTime(1981, 09, 02),
                        PersonalDetailsTypeGender.F,
                        "usi.sample.code+bulk301m@gmail.com",
                        "0900000004",
                        "5 Ae Street",
                        "4013",
                        StateListType.QLD,
                        "NORTHGATE",
                        RequestFactory.Passport()),
                    RequestFactory.CreateApplication(
                        "Tom",
                        "Cruise",
                        new DateTime(1991, 04, 02),
                        PersonalDetailsTypeGender.F,
                        "usi.sample.code+bulk401m@gmail.com",
                        "0900000004",
                        "5 May Street",
                        "4013",
                        StateListType.QLD,
                        "NORTHGATE",
                        RequestFactory.Visa())
                });

                // Open a channel to USI service.
                client = ServiceChannel.OpenWithM2M();

                // Make the USI service call.
                BulkUploadResponse response;
                try
                {
                    response = client.BulkUpload(request);
                }
                catch (FaultException<ErrorInfo> ex)
                {
                    sb.AppendLine("BulkUpload returned a FaultException");
                    sb.AppendLine($"Detail: {ex.Detail.Message}");
                    return sb.ToString();
                }

                return $"Succeeded with receipt number {response.BulkUploadResponse1.ReceiptNumber}";
            }
            finally
            {
                if (client is ICommunicationObject communicationObject)
                {
                    communicationObject.Close();
                }

                ServiceChannel.Close();
            }
        }

        private static string PerformBulkUploadRetrieve(string receiptNumber)
        {
            IUSIService client = null;
            var sb = new StringBuilder();
            try
            {
                // Create a BulkUploadRetrieveRequest using the supplied receipt number.
                var request = new BulkUploadRetrieveType { ReceiptNumber = receiptNumber };
                var wrappedRequest = new BulkUploadRetrieveRequest(request);

                // Open a channel to USI service.
                client = ServiceChannel.OpenWithM2M();

                // Make the USI service call.
                BulkUploadRetrieveResponse response;
                try
                {
                    response = client.BulkUploadRetrieve(wrappedRequest);
                }
                catch (FaultException<ErrorInfo> ex)
                {
                    sb.AppendLine("BulkUploadRetrieve returned a FaultException");
                    sb.AppendLine($"Detail: {ex.Detail.Message}");
                    return sb.ToString();
                }

                // Decode the response messages and display.
                var appResponseStrings = response.BulkUploadRetrieveResponse1.Applications.Select(DecodeBulkUploadRetrievalResponse);
                var lineBreak = string.Format("{0}{0}", Environment.NewLine);
                var messages = string.Join(lineBreak, appResponseStrings);
                return messages;
            }
            finally
            {
                if (client is ICommunicationObject communicationObject)
                {
                    communicationObject.Close();
                }

                ServiceChannel.Close();
            }
        }

        private static VerificationType[] Get500()
        {
            const int numberOfRecords = 500;
            var items = new VerificationType[numberOfRecords];
            for (var i = 0; i < numberOfRecords; i++)
            {
                items[i] = RequestFactory.CreateVerification(
                    i + 1,
                    "DUX9A3FJR6",
                    "Johnfgfgfjdjdjdjdjdjdjdjdjdjdjdjdjdjdjd",
                    "Smitdefdsjdjdjdjdjdjdjdjdjdjdjdjdjdjdjd",
                    new DateTime(1980, 01, 21));
            }

            return items;
        }

        private static string PerformBulkVerify()
        {
            IUSIService client = null;
            var sb = new StringBuilder();
            try
            {
                // Create an array of applications.
                var request = RequestFactory.CreateBulkVerifyRequest(new[]
                {
                    RequestFactory.CreateVerification(1, "C2P5P4UBHP", "Nicholas", "Koke", new DateTime(1990, 07, 02)),
                    RequestFactory.CreateVerification(2, "QS5Q8XWSUJ", "Annie", "Angle", new DateTime(1981, 09, 02)),
                    RequestFactory.CreateVerification(3, "9AKTUJMMAZ", "Lucy", "Smithcd", new DateTime(1985, 03, 03)),
                    RequestFactory.CreateVerification(4, "BJRVU7U59N", "Nick", "Smithdd", new DateTime(1990, 07, 14)),
                    RequestFactory.CreateVerification(5, "VL8CYKH3ND", "Adam", "Smithed", new DateTime(1977, 09, 07)),
                    RequestFactory.CreateVerification(6, "6N69KBFUDZ", "Paul", "Smithfd", new DateTime(1982, 12, 06)),
                    RequestFactory.CreateVerification(7, "N7UEE7FWKV", "Lisa", "Smithgd", new DateTime(1981, 02, 19)),
                    RequestFactory.CreateVerification(8, "A88H9D64CS", "Anne", "Smithhd", new DateTime(1981, 09, 22)),
                    RequestFactory.CreateVerification(9, "GBYDD3ZLVN", "Mary", "Smithid", new DateTime(1991, 04, 26))
                });

                // or use this to create a large request.
                //var request = RequestFactory.CreateBulkVerifyRequest(Get500());

                // Open a channel to USI service.
                client = ServiceChannel.OpenWithM2M();

                // Make the USI service call.
                BulkVerifyUSIResponse response;
                try
                {
                    response = client.BulkVerifyUSI(request);
                }
                catch (FaultException<ErrorInfo> ex)
                {
                    sb.AppendLine("BulkVerifyUSI returned a FaultException");
                    sb.AppendLine($"Detail: {ex.Detail.Message}");
                    return sb.ToString();
                }

                var responseStrings = response.BulkVerifyUSIResponse1.VerificationResponses.Select(DecodeBulkVerifyResponse);
                var lineBreak = string.Format("{0}{0}", Environment.NewLine);
                var messages = string.Join(lineBreak, responseStrings);
                return messages;
            }
            finally
            {
                if (client is ICommunicationObject communicationObject)
                {
                    communicationObject.Close();
                }

                ServiceChannel.Close();
            }
        }

        private static string PerformCreateAndVerifyUsi()
        {
            IUSIService client = null;
            var sb = new StringBuilder();
            try
            {
                // Create an application.
                // See explanation at PerformBulkUpload, above.
                var createRequest = RequestFactory.CreateUsiRequest(RequestFactory.CreateApplication(
                    "Tim",
                    "Rohas",
                    new DateTime(1977, 05, 06),
                    PersonalDetailsTypeGender.F,
                    "usi.sample.code+single1765@gmail.com",
                    "0200001213",
                    "80 Butter Street",
                    "2600",
                    StateListType.ACT,
                    "Canberra",
                    RequestFactory.BirthCert()));

                // Open a channel to USI service.
                client = ServiceChannel.OpenWithM2M();

                // Make the USI service call.
                CreateUSIResponse createResponse;
                try
                {
                    createResponse = client.CreateUSI(createRequest);
                }
                catch (FaultException<ErrorInfo[]> ex)
                {
                    sb.AppendLine("CreateUSI returned a FaultException");
                    sb.AppendLine($"Detail: {ex.Detail.First().Message} {Environment.NewLine}Code: {ex.Detail.First().Code}");
                    return sb.ToString();
                }
                catch (FaultException<ErrorInfo> ex)
                {
                    sb.AppendLine("CreateUSI returned a FaultException");
                    sb.AppendLine($"Detail: {ex.Detail.Message}");
                    return sb.ToString();
                }

                // Note: if the response is "MatchFound", it means the application was rejected based on being a duplicate (try changing the personal details above)
                Console.WriteLine("Application submitted with result: {0}", createResponse.CreateUSIResponse1.Application.Result);

                // Write out any errors that may have occurred during the request.
                if (createResponse.CreateUSIResponse1.Application.Errors != null && createResponse.CreateUSIResponse1.Application.Errors.Length > 0)
                {
                    Console.WriteLine(string.Join(Environment.NewLine, createResponse.CreateUSIResponse1.Application.Errors.Select(e => e.Message)));
                }

                if (createResponse.CreateUSIResponse1.Application.Result != ApplicationResponseTypeResult.Failure)
                {
                    // Create a VerifyUSIRequest to verify the previous call.
                    var verifyRequest = new VerifyUSIType
                    {
                        OrgCode = createRequest.CreateUSI.OrgCode,
                        USI = createResponse.CreateUSIResponse1.Application.USI,
                        Items = createRequest.CreateUSI.Application.PersonalDetails.Items,
                        ItemsElementName = createRequest.CreateUSI.Application.PersonalDetails.ItemsElementName.Select(Translate).ToArray(),
                        DateOfBirth = createRequest.CreateUSI.Application.PersonalDetails.DateOfBirth,
                    };

                    var wrappedVerifyRequest = new VerifyUSIRequest(verifyRequest);

                    // Make the USI service call.
                    VerifyUSIResponse verifyResponse;
                    try
                    {
                        verifyResponse = client.VerifyUSI(wrappedVerifyRequest);
                    }
                    catch (FaultException<ErrorInfo> ex)
                    {
                        sb.AppendLine("VerifyUSIResponse returned a FaultException");
                        sb.AppendLine($"Detail: {ex.Detail.Message}");
                        return sb.ToString();
                    }

                    return $"USI {verifyRequest.USI} verified with status {verifyResponse.VerifyUSIResponse1.USIStatus}";
                }
                else
                {
                    return "Cannot make Verify call for an unsuccessful USI creation.";
                }
            }
            finally
            {
                if (client is ICommunicationObject communicationObject)
                {
                    communicationObject.Close();
                }

                ServiceChannel.Close();
            }
        }

        private static ItemsChoiceType3 Translate(ItemsChoiceType choice)
        {
            return (ItemsChoiceType3) Enum.Parse(typeof(ItemsChoiceType3), choice.ToString());
        }
    }
}