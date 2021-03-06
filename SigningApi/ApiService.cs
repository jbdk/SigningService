﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using ClrPlus.Core.Extensions;
using Microsoft.WindowsAzure.Storage.Blob;
using Outercurve.DTO.Request;
using Outercurve.DTO.Response;
using Outercurve.DTO.Services.Azure;
using Outercurve.SigningApi.Signers;
using ServiceStack.Configuration;
using ServiceStack.ServiceInterface;

namespace Outercurve.SigningApi
{
    public class ApiService : Service
    {
        private readonly IAzureService _azure;
        private readonly FsService _fs;
        private readonly CertService _certs;
        private readonly AppSettings _settings;
        private readonly CustomBasicAuthProvider _authProvider;
        private readonly List<String> Errors = new List<string>();
        private readonly LoggingService _log;
        public const int HOURS_FILE_SHOULD_BE_ACCESSIBLE = 12;

        public ApiService(AzureClient azure, FsService fs, CertService certs, AppSettings settings, CustomBasicAuthProvider authProvider, LoggingService log)
        {
            _azure = azure.GetRoot();
            _fs = fs;
            _certs = certs;
            _settings = settings;
            _authProvider = authProvider;
            _log = log;
        }

        public GetUploadLocationResponse Post(GetUploadLocationRequest request)
        {
            _log.StartLog(request);
            try
            {

            
                var contName = "deletable" + Guid.NewGuid().ToString("D").ToLowerInvariant();

                var container = _azure.CreateContainerIfDoesNotExist(contName);

                var blobPolicy = new SharedAccessBlobPolicy {
                                                                Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.List,
                                                                SharedAccessExpiryTime = DateTimeOffset.Now.AddHours(HOURS_FILE_SHOULD_BE_ACCESSIBLE)
                                                            
                                                            };

                var permissions = new BlobContainerPermissions();
                permissions.SharedAccessPolicies.Add("mypolicy", blobPolicy);
                permissions.PublicAccess = BlobContainerPublicAccessType.Container;
                container.SetPermissions(permissions);
                var sharedAccessSignature = container.GetSharedAccessSignature("mypolicy");

                return new GetUploadLocationResponse {Name= container.Name, Location = container.Uri.ToString(), Sas = sharedAccessSignature, Account = _azure.Account};
            }
            catch (Exception e)
            {
                _log.Fatal("error", e);
                Errors.Add(e.Message + " " + e.StackTrace);

                return new GetUploadLocationResponse {Errors = Errors};
            }
        }



        public SetCodeSignatureResponse Post(SetCodeSignatureRequest request)
        {
            _log.StartLog(request);
           
            
            //Errors.Add(" cert is " + cert.SerialNumber);
            try
            {
                var tempPath = CopyFileToTemp(request.Container, request.Path);
                var cert = _certs.Get(_settings.GetString("CertificatePath"));
                if (FileSensing.IsItAZipFile(tempPath))
                {
                    AttemptToSignOPC(tempPath, cert);
                    _log.Debug("OPC Signing is done");
                }
                else
                {
                    AttemptToSignAuthenticode(tempPath, request.StrongName, cert);
                    _log.Debug("Authenticode is done");
                  
                }
                _log.Debug(@"let's copy the file from {0} to {1}\{2}".format(tempPath, request.Container, request.Path));
                CopyFileToAzure(request.Container, request.Path, tempPath);
                return new SetCodeSignatureResponse();
            
            }
            catch (Exception e)
            {
                _log.Fatal("error", e);
                Errors.Add(e.Message + " " + e.StackTrace);
                return new SetCodeSignatureResponse { Errors = Errors };
            }
        }

        public BaseResponse Post(SetRolesRequest request)
        {
            _log.StartLog(request);
            try
            {
                _authProvider.SetRoles(request.UserName, request.Roles.ToArray());
                return new BaseResponse();
            }
            catch (Exception e)
            {
                _log.Fatal("error", e);
                Errors.Add(e.Message + " " + e.StackTrace);
                return new BaseResponse { Errors = Errors };
            }
                

        }


        public GetRolesResponse Post(GetRolesAsAdminRequest request)
        {
            _log.StartLog(request);
            try
            {
                var roles = _authProvider.GetRoles(request.UserName);
                return new GetRolesResponse {Roles = roles.ToList()};
            }
            catch (Exception e)
            {
                _log.Fatal("error", e );
                Errors.Add(e.Message + " " + e.StackTrace);
                return new GetRolesResponse {Errors = Errors};
            }
        }

        public GetRolesResponse Post(GetRolesRequest request)
        {
            _log.StartLog(request);
            try
            {

                var roles = _authProvider.GetRoles(this.GetSession().UserAuthName);
                return new GetRolesResponse {Roles = roles.ToList()};
            }
            catch (Exception e)
            {
                _log.Fatal("error", e);
                Errors.Add(e.Message + " " + e.StackTrace);
                return new GetRolesResponse {Errors = Errors};
            }
        }

        public BaseResponse Post(UnsetRolesRequest request)
        {
            _log.StartLog(request);
            try
            {
                _authProvider.UnsetRoles(request.UserName, request.Roles.ToArray());
                return new BaseResponse();
            }
            catch (Exception e)
            {
                _log.Fatal("error", e);
                Errors.Add(e.Message + " " + e.StackTrace);
                return new SetCodeSignatureResponse { Errors = Errors };
            }
                
        }

        public CreateUserResponse Post(ResetPasswordAsAdminRequest request)
        {
            _log.StartLog(request);
            try
            {
                var pass = _authProvider.ResetPasswordAsAdmin(request.UserName);
                return new CreateUserResponse {Password = pass};
            }
            catch (Exception e)
            {
                _log.Fatal("error", e);
                Errors.Add(e.Message + " " + e.StackTrace);
                return new CreateUserResponse { Errors = Errors };
            }
        }

        public BaseResponse Post(RemoveUserRequest request)
        {
            _log.StartLog(request);
            try
            {
                _authProvider.RemoveUser(request.UserName);
                return new BaseResponse();
            }
            catch (Exception e)
            {
                _log.Fatal("error", e);
                Errors.Add(e.Message + " " + e.StackTrace);
                return new BaseResponse { Errors = Errors };
            }
        }

        public BaseResponse Post(SetPasswordRequest request)
        {
            _log.StartLog(request);
            try
            {
                _authProvider.SetPassword(this.GetSession().UserAuthName, request.NewPassword);
                return new BaseResponse();
            }
            catch (Exception e)
            {
                _log.Fatal("error", e);
                Errors.Add(e.Message + " " + e.StackTrace);
                return new CreateUserResponse { Errors = Errors };
            }
        }

        public CreateUserResponse Post(CreateUserRequest request)
        {
            _log.StartLog(request);
            try
            {
                if (String.IsNullOrWhiteSpace(request.Password))
                {
                    var password = _authProvider.CreateUser(request.Username);
                    if (password != null)
                    {
                        return new CreateUserResponse {Password = password};
                    }
                        throw new Exception("User could not be created. May already be registered?");
                    

                }
                else
                {
                   if (_authProvider.CreateUserWithPassword(request.Username, request.Password))
                   {
                       return new CreateUserResponse();
                   }

                   throw new Exception("User could not be created. May already be registered?");
                    
                }
            }
            catch (Exception e)
            {
                _log.Fatal("error", e);
                Errors.Add(e.Message + " " + e.StackTrace);
                return new CreateUserResponse {Errors = Errors};
            }
        }

        public BaseResponse Post(InitializeRequest request)
        {
            _log.StartLog(request);
            try
            {
                _authProvider.Initialize(request.UserName, request.Password);
                return new BaseResponse();
            }
            catch (Exception e)
            {
                _log.Fatal("error", e);
                Errors.Add(e.Message + " " + e.StackTrace);
                return new BaseResponse { Errors = Errors };
            }
        }

        private void AttemptToSignAuthenticode(string path, bool strongName, X509Certificate2 certificate)
        {
           
            //_log.Debug(path);
            //_log.Debug(strongName);
            //_log.Debug(certificate);
            
            var authenticode = new AuthenticodeSigner(certificate, _log);
            AttemptToSign(() => authenticode.Sign(path, strongName));
           
        }

        private void AttemptToSignOPC(string path, X509Certificate2 certificate)
        {

            var opc = new OPCSigner(certificate, _log);

            AttemptToSign(() => opc.Sign(path, true));
        }

        private void AttemptToSign(Action signAction)
        {
           /* try
            {*/
                signAction();
               
            /*}
            catch (FileNotFoundException fnfe)
            {
                ThrowTerminatingError(new ErrorRecord(fnfe, "none", ErrorCategory.OpenError, null));
            }
            catch (PathTooLongException ptle)
            {
                ThrowTerminatingError(new ErrorRecord(ptle, "none", ErrorCategory.InvalidData, null));
            }
            catch (UnauthorizedAccessException uae)
            {
                ThrowTerminatingError(new ErrorRecord(uae, "none", ErrorCategory.PermissionDenied, null));
            }
            catch (Exception e)
            {
                ThrowTerminatingError(new ErrorRecord(e, "none", ErrorCategory.NotSpecified, null));
            }*/
           // return false;
        }

       private string CopyFileToTemp(string container, string file)
       {
           var blob  = GetBlob(container, file);
           if (blob == null)
               return null;
           
           var temp = _fs.CreateTempPath(file.Replace('/', '_'));
          
           using (var blobStream = blob.OpenRead())
           {
               using (var tempFile = _fs.OpenWrite(temp))
               {
                   blobStream.CopyTo(tempFile);
               }
           }

           return temp;
       }

        private void CopyFileToAzure(string container, string file, string tempFile)
        {
           var blob  = GetBlob(container, file);

           using (var fileStream = _fs.OpenRead(tempFile))
           {
               using (var blobStream = blob.OpenWrite())
               {
                   fileStream.CopyTo(blobStream);
               }
           }
        }

        private IAzureBlob GetBlob(string container, string fileName)
        {
            var cont = _azure.Containers.FirstOrDefault(c => c.Name == container);
            if (cont == null)
                return null;

            var file = cont.Files.FirstOrDefault(f => f.Name == fileName);
            return file;
        }
    }
}