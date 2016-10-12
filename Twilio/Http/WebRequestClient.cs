﻿using System;
using System.IO;
using System.Net;
using Twilio.Exceptions;
using Newtonsoft.Json;

#if NET40
using System.Threading.Tasks;
#endif

namespace Twilio.Http
{
	public class WebRequestClient : HttpClient
	{
	    private static Exception HandleErrorResponse(HttpWebResponse errorResponse)
	    {
	        if (errorResponse.StatusCode >= HttpStatusCode.InternalServerError &&
	            errorResponse.StatusCode < HttpStatusCode.HttpVersionNotSupported)
	        {
	            return new TwilioException("Internal Server error: " + errorResponse.StatusDescription);
	        }

	        var responseStream = errorResponse.GetResponseStream();
	        var errorReader = new StreamReader(responseStream);
	        var errorContent = errorReader.ReadToEnd();

	        try
	        {
	            var restEx = RestException.FromJson(errorContent);
	            return restEx ?? new TwilioException("Error: " + errorResponse.StatusDescription + " - " + errorContent);
	        }
	        catch (JsonReaderException)
	        {
	            return new TwilioException("Error: " + errorResponse.StatusDescription + " - " + errorContent);
	        }
	    }

	    public override Response MakeRequest(Request request) {
			var httpRequest = (HttpWebRequest) WebRequest.Create(request.ConstructUrl());
			httpRequest.Method = request.Method.ToString();
			httpRequest.Accept = "application/json";
			httpRequest.Headers["Accept-Encoding"] = "utf-8";

			var authBytes = Authentication(request.Username, request.Password);
			httpRequest.Headers["Authorization"] = "Basic " + authBytes;
			httpRequest.ContentType = "application/x-www-form-urlencoded";

//		    var version = Assembly.GetExecutingAssembly().GetName().Version;
//		    httpRequest.UserAgent = "twilio-csharp/" + version + " (.NET " + Environment.Version.ToString() + ")";

			if (!Equals(request.Method, HttpMethod.GET)) {

			    #if NET40
			    var streamTask = Task.Factory.FromAsync<Stream>(
			        httpRequest.BeginGetRequestStream,
			        httpRequest.EndGetRequestStream,
			        null
			    );
			    streamTask.Wait();
			    #endif

                #if NET40
				var stream = streamTask.Result;
				#else
				var stream = httpRequest.GetRequestStream();
				#endif

			    stream.Write(request.EncodePostParams(), 0, request.EncodePostParams().Length);
			}

			try
			{

				#if NET40
				var responseTask = Task.Factory.FromAsync<System.Net.WebResponse>(
				    httpRequest.BeginGetResponse,
				    httpRequest.EndGetResponse,
				    null
				);
				responseTask.Wait();
			    #endif

			    #if NET40
				var response = (HttpWebResponse) responseTask.Result;
				#else
				var response = (System.Net.HttpWebResponse) httpRequest.GetResponse();
				#endif

			    var responseStream = response.GetResponseStream();
			    var reader = new StreamReader(responseStream);
				return new Response(response.StatusCode, reader.ReadToEnd());
			}
            #if NET40
			catch (AggregateException ae)
			{
				ae.Handle ((x) => {
				    if (!(x is WebException))
				    {
				        return false;
				    }

				    var e = (WebException) x;
				    throw HandleErrorResponse((HttpWebResponse) e.Response);
				});

                return null;
			}
			#else
            catch (WebException e)
            {
                throw HandleErrorResponse((HttpWebResponse) e.Response);
			}
			#endif
	    }
	}
}