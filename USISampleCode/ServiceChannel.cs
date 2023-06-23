using System;
using System.Configuration;
using System.Globalization;
using System.IdentityModel.Protocols.WSTrust;
using System.IdentityModel.Tokens;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Configuration;
using System.ServiceModel.Security;
using System.Xml.Serialization;
using System.Xml;
using Abr.AuskeyManager.KeyStore;
using USISampleCode.USIServiceReference;
using System.ServiceModel.Channels;
using System.Collections.Generic;
using System.Xml.Schema;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Runtime.Remoting.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace USISampleCode
{
    internal static class ServiceChannel
    {
        // Obviously these two variables should be in a config file. Included here so it's easier to see how this code works.
        private const string ClientCertificateKeystoreLocation = "keystore-usi.xml"; // @"C:\Developer\Userdata\keystore.xml";
        private const string Alias = "ABRD:27809366375_USIMachine"; // Old one: "ABRD:12300000059_TestDevice03";

        // This should be stored in encrypted form. See notes in GetPasswordString().
        private const string Password = "Password1!";

        private static ChannelFactory<IUSIService> _channelFactory;

        public static Properties MyProperties = new Properties();
        public static IUSIService OpenWithM2M()
        {
            var token = GetStsToken(60);
            var clientSection = (ClientSection)ConfigurationManager.GetSection("system.serviceModel/client");
            var endpointElement = clientSection.Endpoints.OfType<ChannelEndpointElement>()
                .First(endpoint =>
                string.Equals("USIServiceReference.IUSIService",
                endpoint.Contract, StringComparison.OrdinalIgnoreCase));
            if (endpointElement == null)
            {
                throw new Exception("No endpoint matching service contract was found");
            }

            var channelFactory = new ChannelFactory<IUSIService>(endpointElement.Name);
            channelFactory.Open();
            return channelFactory.CreateChannelWithIssuedToken(token);
        }
        private static SecurityToken GetStsToken(int tokenLifeTimeMinutes)
        {
            var factory = new WSTrustChannelFactory("S007SecurityTokenServiceEndpoint");
            factory.Endpoint.Behaviors.Add(new InspectorBehavior(new LoggingMessageInspector()));

            if (factory.Credentials != null)
            {
                factory.Credentials.ClientCertificate.Certificate = GetClientCertificateFromKeystore();

                // Instantiate and invoke the client to get the security token
                factory.Credentials.SupportInteractive = false;
            }

            var appliesTo = ConfigurationManager.AppSettings["appliesTo"];
            var rst = new RequestSecurityToken
            {
                Claims =
                {
                    new RequestClaim("http://vanguard.ebusiness.gov.au/2008/06/identity/claims/abn", false),
                    new RequestClaim("http://vanguard.ebusiness.gov.au/2008/06/identity/claims/credentialtype", false)
                },
                AppliesTo = new EndpointReference(appliesTo),
                Lifetime = new Lifetime(DateTime.UtcNow, DateTime.UtcNow.AddMinutes(tokenLifeTimeMinutes)),
                RequestType = RequestTypes.Issue,
                KeyType = KeyTypes.Symmetric,
                TokenType = "http://docs.oasis-open.org/wss/oasis-wss-saml-token-profile-1.1#SAMLV1.1",
                // ActAs = This SecurityTokenElement need to be passed by the Host Service Providers.  
            };

            // Instantiate and invoke the client to get the security token
            // create the channel for the security token service
            var client = (WSTrustChannel)factory.CreateChannel();
            /*
            using(var sw = new StringWriter())
            {
                var serializer = new XmlSerializer(rst.GetType(), "http://schemas.xmlsoap.org/ws/2005/02/trust");
                var xws = new XmlWriterSettings { OmitXmlDeclaration = true };
                using (var xw = XmlWriter.Create(sw, xws))
                {
                    serializer.Serialize(xw, rst, new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty }));
                }
                var xml = sw.ToString();
                Console.WriteLine(xml); // This will print out the XML of the request
            }
            */
            
            //test.WriteXml(rst);
            /*
            foreach (PropertyInfo property in rst.GetType().GetProperties())
            {
                if (property.CanRead)
                {
                    object value = property.GetValue(rst, null);
                    string name = property.Name;

                    MyProperties.Add(name, value);
                }
            }
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.ConformanceLevel = ConformanceLevel.Auto;

            XmlWriter writer = XmlWriter.Create("output.xml", settings);
            MyProperties.WriteXml(writer);
            */
            // var requestXml = requestMessage.ToString();
            // Console.WriteLine(requestXml);
            var response = client.Issue(rst);
            /*
            // Serialize the message to get the SOAP request XML
            string xmlRequest;
            using (var writer = new StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(writer))
                {
                    message.WriteMessage(xmlWriter);
                    xmlWriter.Flush();
                    xmlRequest = writer.ToString();
                }
            }
            */
            return response;
        }

        private static X509Certificate2 GetClientCertificateFromKeystore()
        {
            var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName();

            // Please replace [YourCompanyName] tag with the valid organisation Name
            AbrProperties.SetSoftwareInfo("[OrganisationName]", entryAssemblyName?.Name, entryAssemblyName?.Version.ToString(), DateTime.Now.ToString(CultureInfo.InvariantCulture));
            var keyStore = new AbrKeyStore(File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ClientCertificateKeystoreLocation)));
            using (var pwd = GetPasswordString())
            {
                var abrCredential = keyStore.GetCredential(Alias);
                if (!abrCredential.IsReadyForRenewal())
                {
                    var clientCertificate = abrCredential.PrivateKey(pwd, X509KeyStorageFlags.MachineKeySet);
                    return clientCertificate;
                }
                else
                {
                    // throw new Exception("Renew certificate");
                    var clientCertificate = abrCredential.PrivateKey(pwd, X509KeyStorageFlags.MachineKeySet);
                    return clientCertificate;
                }
            }
        }

        private static SecureString GetPasswordString()
        {
            // NOTE: This code is for demonstration purposes only.
            //      Production code should obtain the password SecureString instance from an encrypted source,
            //      and it should never be held in a plain String object.
            //      Read MSDN remarks about why to avoid the password being stored in a plain String object: 
            //      http://msdn.microsoft.com/en-us/library/system.security.securestring%28v=vs.110%29.aspx
            var pwd = new SecureString();
            foreach (var c in Password)
            {
                pwd.AppendChar(c);
            }

            return pwd;
        }

        public static void Close()
        {
            _channelFactory?.Close();
            _channelFactory = null;
        }
    }
    public class Properties : IXmlSerializable
    {
        private Dictionary<string, object> propDict = new Dictionary<string, object>();

        public void Add(string key, object value)
        {
            propDict.Add(key, value);
        }

        public object this[string key]
        {
            get => propDict[key];
            set => propDict[key] = value;
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Not needed
        }

        public static bool IsSerializable(Type type)
        {
            return type.IsSerializable;
        }
        public void WriteXml(XmlWriter writer)
        {
            var keySerializer = new XmlSerializer(typeof(string));
            var valueSerializer = new XmlSerializer(typeof(object));

            serializeValue(propDict, writer, keySerializer, valueSerializer);

        }
        public void serializeValue(Dictionary<string, object> dicValue, XmlWriter writer, XmlSerializer keySerializer, XmlSerializer valueSerializer)
        {
            foreach (var keyValuePair in dicValue)
            {
                Console.WriteLine("key: " + keyValuePair);
                Console.WriteLine("value: " + dicValue);
                writer.WriteStartElement("item");
                writer.WriteStartElement("key");
                keySerializer.Serialize(writer, keyValuePair.Key);

                writer.WriteEndElement();
                writer.WriteStartElement("value");
                // You need to handle the case where value is not serializable.
                // If unsure, write it as string.
                if (keyValuePair.Value != null)
                {
                    var value = keyValuePair.Value;

                    if (value is Dictionary<string, object> dictionary)
                    {
                        serializeValue(dictionary, writer, keySerializer, valueSerializer);
                        continue;
                    }
                    // Console.WriteLine(value.GetType());
                    if (value is RequestClaimCollection requestClaimCollection)
                    {
                        var serializableCollection = new Collection<SerializableRequestClaim>();
                        foreach (var requestClaim in requestClaimCollection)
                        {
                            var serializableRequestClaim = new SerializableRequestClaim(requestClaim);
                            serializableCollection.Add(serializableRequestClaim);
                        }

                        value = serializableCollection;
                        Console.WriteLine(value);
                    }

                    if (value is EndpointReference endpointReference)
                    {
                        value = new SerializableEndpointReference(endpointReference);
                    }

                    if (value is Lifetime lifetime)
                    {
                        value = new SerializableLifetime(lifetime);
                    }

                    var valueType = value.GetType();
                    valueSerializer = new XmlSerializer(valueType);

                    valueSerializer.Serialize(writer, value);

                }
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
        }
        
    }

    public class SerializableRequestClaim
    {
        public string ClaimType { get; set; }
        public bool? IsOptional { get; set; }

        // Add other properties as needed...

        public SerializableRequestClaim() { }

        public SerializableRequestClaim(RequestClaim requestClaim)
        {
            this.ClaimType = requestClaim.ClaimType;
            this.IsOptional = requestClaim.IsOptional;

            // Copy other properties as needed...
        }
    }

    public class SerializableEndpointReference
    {
        public Collection<XmlElement> Details { get; set; }
        // public Uri Uri { get; set;}
        public SerializableEndpointReference() { }

        public SerializableEndpointReference(EndpointReference endpointReference)
        {
            this.Details = endpointReference.Details;
            // this.Uri = endpointReference.Uri;
        }
    }

    public class SerializableLifetime
    {
        public SerializableLifetime() { }
        public SerializableLifetime(Lifetime lifetime) { }
    }
    public class LoggingMessageInspector : IClientMessageInspector
    {
        // Implement the methods of IClientMessageInspector as described in the previous message
        // ...
        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            var buffer = reply.CreateBufferedCopy(Int32.MaxValue);
            var message = buffer.CreateMessage();

            string messageContent = message.ToString();
            Console.WriteLine(messageContent);
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            return request;
        }
    }

    public class InspectorBehavior : IEndpointBehavior
    {
        private readonly IClientMessageInspector _inspector;

        public InspectorBehavior(IClientMessageInspector inspector)
        {
            _inspector = inspector;
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters) { }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.MessageInspectors.Add(_inspector);
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher) { }

        public void Validate(ServiceEndpoint endpoint) { }
    }
}
