using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Net;
using PDFCloudPrintingDemo.Classes;
using Newtonsoft.Json;
using PDFCloudPrintingDemo.SessionModels;
using DotNetOpenAuth.GoogleOAuth2;
using Microsoft.AspNet.Membership.OpenAuth;
using System.Collections.Specialized;

namespace PDFCloudPrintingDemo.Controllers
{
    public class HomeController : Controller
    {
        public ISessionWrapper SessionWrapper = new HttpContextSessionWrapper();
        private ConfigReader configReader = new ConfigReader();
        private AppSettings appSettings = new AppSettings();
        string tempPath = Path.GetTempPath() + "\\PDFCloudPrintingDemo\\";
        //SessionWrapper = new HttpContextSessionWrapper();

        public ActionResult Index()
        {
            if (SessionWrapper.loginUserName == null)
                return View("Login");
            else
                return View();
        }

        public ActionResult Login()
        {
            return View();
        }

        public ActionResult filesUpload(List<HttpPostedFileBase> files)
        {
            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);

            if (files != null)
            {
                foreach (var file in files)
                {
                    // Some browsers send file names with full path.
                    // We are only interested in the file name.
                    var fileName = Path.GetFileName(file.FileName);
                    var physicalPath = Path.Combine(tempPath, fileName);

                    // The files are not actually saved in this demo
                     file.SaveAs(physicalPath);
                }

                submitFiles();
            }

            return Content("");
        }

        public ActionResult filesRemove(string[] fileNames)
        {
            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);

            if (fileNames != null)
            {
                foreach (var fullName in fileNames)
                {
                    var fileName = Path.GetFileName(fullName);
                    var physicalPath = Path.Combine(tempPath, fileName);

                    if (System.IO.File.Exists(physicalPath))
                    {
                        // The files are not actually removed in this demo
                        System.IO.File.Delete(physicalPath);
                    }
                }
            }

            return Content("");
        }

        public ActionResult submitFiles()
        {
            foreach (var key in ModelState.Keys.ToList().Where(key => ModelState.ContainsKey(key)))
            {
                ModelState.Remove(key); //This was my solution before
                //ModelState[key].Errors.Clear(); //This is my new solution. Thanks bbak
            }

            DirectoryInfo directoryInfo = new DirectoryInfo(tempPath);
            FileInfo[] fileInfos = directoryInfo.GetFiles();

            if (fileInfos.Count() == 0)
            {
                ModelState.AddModelError("Error", "Error: No file uploaded or invalid file type!");
                return View("Index");
            }
            else
            {
                //GetFileInfo(fileInfos);
                ModelState.AddModelError("Message", "Sent to printer!");
                return View("Index");
            }
        }

        private List<byte[]> GetFileInfo (FileInfo[] files) //(List<HttpPostedFileBase> files)
        {
            List<byte[]> byteArrayList = new List<byte[]>();

            for (int i = 0; i < files.Count(); i ++)
            {
                byte[] fileData = System.IO.File.ReadAllBytes(files[i].FullName);

                string AsBase64String = Convert.ToBase64String(fileData);
                SendPrintPDF(AsBase64String, files[i].Name, true);

                byte[] tempBytes = Convert.FromBase64String(AsBase64String);
                byteArrayList.Add(tempBytes);

                var fileName = Path.GetFileName(files[i].FullName);
                var physicalPath = Path.Combine(tempPath, fileName);

                if (System.IO.File.Exists(physicalPath))
                {
                    // The files are not actually removed in this demo
                    System.IO.File.Delete(physicalPath);
                }
            }

            return byteArrayList;
        }

        private string SendPrintPDF(string CMD, string DocumentName, bool isPrintSpooler)
        {
            appSettings.ServerURL = configReader.GetConfigValue("appSettings", "SASURL");
            appSettings.MACAddress = configReader.GetConfigValue("appSettings", "PrinterMACAdd");

            string result = "";
            var sendPrintCMDURL = "/SATOCloudPrinting/WebAPI/SendPrintCmd?mac=" + appSettings.MACAddress + "&encoding=ANSI&documentName=" + DocumentName + "&isPrintSpooler=" + isPrintSpooler;
            string BaseURL = appSettings.ServerURL + sendPrintCMDURL;

            byte[] byteData = System.Convert.FromBase64String(CMD);

            HttpWebRequest sendPrintCMDRequest = (HttpWebRequest)WebRequest.Create(BaseURL);
            sendPrintCMDRequest.Method = "POST";
            sendPrintCMDRequest.ContentType = "application/x-www-form-urlencoded";
            sendPrintCMDRequest.ContentLength = byteData.Length;

            Stream newStream = sendPrintCMDRequest.GetRequestStream();
            newStream.Write(byteData, 0, byteData.Length);
            newStream.Close();

            WebResponse sendPrintCMDWebResponse = sendPrintCMDRequest.GetResponse();
            using (Stream sendPrintCMDRequestWebStream = sendPrintCMDWebResponse.GetResponseStream() ?? Stream.Null)
            using (StreamReader sendPrintCMDResponseReader = new StreamReader(sendPrintCMDRequestWebStream))
            {
                result = sendPrintCMDResponseReader.ReadToEnd();
            }

            return result;
        }

        public ActionResult TestPrint()
        {
            appSettings.ServerURL = configReader.GetConfigValue("appSettings", "SASURL");
            appSettings.MACAddress = configReader.GetConfigValue("appSettings", "PrinterMACAdd");

            string result = "";
            var testPrintURL = "/SATOCloudPrinting/WebAPI/TestPrint?mac=" + appSettings.MACAddress + "&isPrintSpooler=true";
            string BaseURL = appSettings.ServerURL + testPrintURL;

            HttpWebRequest testPrintRequest = (HttpWebRequest)WebRequest.Create(BaseURL);
            testPrintRequest.Method = "POST";
            testPrintRequest.ContentType = "application/json";
            testPrintRequest.ContentLength = 0;

            WebResponse testPrintWebResponse = testPrintRequest.GetResponse();
            using (Stream testPrintRequestWebStream = testPrintWebResponse.GetResponseStream() ?? Stream.Null)
            using (StreamReader testPrintResponseReader = new StreamReader(testPrintRequestWebStream))
            {
                result = testPrintResponseReader.ReadToEnd();
            }

            //return result;

            ModelState.AddModelError("Message", "Sent to printer!");
            return View("Index");
        }

        public ActionResult RedirectToGoogle()
        {
            string provider = "google";
            string returnUrl = "";
            return new ExternalLoginResult(provider, Url.Action("ExternalLoginCallback", new { ReturnUrl = returnUrl }));
        }

        public ActionResult LogOut()
        {
            SessionWrapper.loginUserName = null;
            return View("Login");
        }

        [AllowAnonymous]
        public ActionResult ExternalLoginCallback(string returnUrl)
        {
            foreach (var key in ModelState.Keys.ToList().Where(key => ModelState.ContainsKey(key)))
            {
                ModelState.Remove(key); //This was my solution before
                //ModelState[key].Errors.Clear(); //This is my new solution. Thanks bbak
            }

            string ProviderName = OpenAuth.GetProviderNameFromCurrentRequest();

            if (ProviderName == null || ProviderName == "")
            {
                NameValueCollection nvs = Request.QueryString;
                if (nvs.Count > 0)
                {
                    if (nvs["state"] != null)
                    {
                        NameValueCollection provideritem = HttpUtility.ParseQueryString(nvs["state"]);
                        if (provideritem["__provider__"] != null)
                        {
                            ProviderName = provideritem["__provider__"];
                        }
                    }
                }
            }

            GoogleOAuth2Client.RewriteRequest();

            var redirectUrl = Url.Action("ExternalLoginCallback", new { ReturnUrl = returnUrl });
            var retUrl = returnUrl;

            var authResult = OpenAuth.VerifyAuthentication(redirectUrl);

            if (!authResult.IsSuccessful)
            {
                ModelState.AddModelError("Error", "Error: Login failed. Please try again!");
                return View("Login");
                //return Redirect(Url.Action("Login", "Home"));
            }
            else
            {
                string login_email = authResult.ExtraData["email"];

                if (!login_email.Contains("sato-global.com"))
                {
                    ModelState.AddModelError("Error", "Error: Please log in with your SATO email!");

                    return View("Login");
                    //return Redirect(Url.Action("Login", "Home"));
                }
                else
                {
                    SessionWrapper = new HttpContextSessionWrapper();
                    SessionWrapper.loginUserName = authResult.ExtraData["name"];
                    return Redirect(Url.Action("Index", "Home"));
                }
            }

            //// User has logged in with provider successfully
            //// Check if user is already registered locally
            ////You can call you user data access method to check and create users based on your model
            //if (OpenAuth.Login(authResult.Provider, authResult.ProviderUserId, createPersistentCookie: false))
            //{
            //    return Redirect(Url.Action("Index", "Home"));
            //}

            ////Get provider user details
            //string ProviderUserId = authResult.ProviderUserId;
            //string ProviderUserName = authResult.UserName;

            //string Email = null;
            //if (Email == null && authResult.ExtraData.ContainsKey("email"))
            //{
            //    Email = authResult.ExtraData["email"];
            //}

            //if (User.Identity.IsAuthenticated)
            //{
            //    // User is already authenticated, add the external login and redirect to return url
            //    OpenAuth.AddAccountToExistingUser(ProviderName, ProviderUserId, ProviderUserName, User.Identity.Name);
            //    return Redirect(Url.Action("Index", "Home"));
            //}
            //else
            //{
            //    // User is new, save email as username
            //    string membershipUserName = Email ?? ProviderUserId;
            //    var createResult = OpenAuth.CreateUser(ProviderName, ProviderUserId, ProviderUserName, membershipUserName);

            //    if (!createResult.IsSuccessful)
            //    {
            //        ViewBag.Message = "User cannot be created";
            //        return View();
            //    }
            //    else
            //    {
            //        // User created
            //        if (OpenAuth.Login(ProviderName, ProviderUserId, createPersistentCookie: false))
            //        {
            //            return Redirect(Url.Action("Index", "Home"));
            //        }
            //    }
            //}

            //return View();
        }

        internal class ExternalLoginResult : ActionResult
        {
            public ExternalLoginResult(string provider, string returnUrl)
            {
                Provider = provider;
                ReturnUrl = returnUrl;
            }

            public string Provider { get; private set; }
            public string ReturnUrl { get; private set; }

            public override void ExecuteResult(ControllerContext context)
            {
                OpenAuth.RequestAuthentication(Provider, ReturnUrl);
            }
        }
    }
}
