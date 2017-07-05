using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using TestFileUpload.Filters;
using TestFileUpload.Helper;
using TestFileUpload.Models;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860
namespace TestFileUpload.Controllers
{
    public class StreamingController : Controller
    {

        private static readonly FormOptions _defaultFormOptions = new FormOptions();

        // GET: /<controller>/
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [DisableFormValueModelBinding]
        [ValidateAntiForgeryToken]
        public async Task< IActionResult > StreamUpload()
        {
            if( !MultipartRequestHelper.IsMultipartContentType( Request.ContentType ) ) {
                return BadRequest( $"Expected a multipart request, but got {Request.ContentType}" );
            }

            // Used to accumulate all the form url encoded key value pairs in the 
            // request.
            var formAccumulator = new KeyValueAccumulator();
            string targetFilePath = null;

            var boundary = MultipartRequestHelper.GetBoundary( MediaTypeHeaderValue.Parse( Request.ContentType ),
                                                               _defaultFormOptions.MultipartBoundaryLengthLimit );
            var reader = new MultipartReader( boundary,
                                              HttpContext.Request.Body );

            var section = await reader.ReadNextSectionAsync();
            while( section != null ) {
                ContentDispositionHeaderValue contentDisposition;
                var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse( section.ContentDisposition,
                                                                                          out contentDisposition );

                if( hasContentDispositionHeader ) {
                    if( MultipartRequestHelper.HasFileContentDisposition( contentDisposition ) ) {
                        targetFilePath = Path.GetTempFileName();
                        using( var targetStream = System.IO.File.Create( targetFilePath ) ) {
                            await section.Body.CopyToAsync( targetStream );

                            //_logger.LogInformation( $"Copied the uploaded file '{targetFilePath}'" );
                        }
                    } else if( MultipartRequestHelper.HasFormDataContentDisposition( contentDisposition ) ) {
                        // Content-Disposition: form-data; name="key"
                        //
                        // value

                        // Do not limit the key name length here because the 
                        // multipart headers length limit is already in effect.
                        var key = HeaderUtilities.RemoveQuotes( contentDisposition.Name );
                        var encoding = GetEncoding( section );
                        using( var streamReader = new StreamReader( section.Body,
                                                                    encoding,
                                                                    detectEncodingFromByteOrderMarks : true,
                                                                    bufferSize : 1024,
                                                                    leaveOpen : true ) ) {
                            // The value length limit is enforced by MultipartBodyLengthLimit
                            var value = await streamReader.ReadToEndAsync();
                            if( String.Equals( value,
                                               "undefined",
                                               StringComparison.OrdinalIgnoreCase ) ) {
                                value = String.Empty;
                            }
                            formAccumulator.Append( key,
                                                    value );

                            if( formAccumulator.ValueCount > _defaultFormOptions.ValueCountLimit ) {
                                throw new
                                                InvalidDataException( $"Form key count limit {_defaultFormOptions.ValueCountLimit} exceeded." );
                            }
                        }
                    }
                }

                // Drains any remaining section body that has not been consumed and
                // reads the headers for the next section.
                section = await reader.ReadNextSectionAsync();
            }

            // Bind form data to a model
            var requestedFileUpload = new FileUploadRequest();
            var formValueProvider = new FormValueProvider( BindingSource.Form,
                                                           new FormCollection( formAccumulator.GetResults() ),
                                                           CultureInfo.CurrentCulture );

            var bindingSuccessful = await TryUpdateModelAsync( requestedFileUpload,
                                                               prefix : "",
                                                               valueProvider : formValueProvider );
            if( !bindingSuccessful ) {
                if( !ModelState.IsValid ) {
                    return BadRequest( ModelState );
                }
            }

            var uploadedData = new FileUploadRequestResult() {
                Name = requestedFileUpload.Name,
                Age = requestedFileUpload.Age,
                Zipcode = requestedFileUpload.Zipcode,
                FilePath = targetFilePath
            };
            return Json( uploadedData );
        }

        private static Encoding GetEncoding( MultipartSection section ) {
            MediaTypeHeaderValue mediaType;
            var hasMediaTypeHeader = MediaTypeHeaderValue.TryParse( section.ContentType,
                                                                    out mediaType );

            // UTF-7 is insecure and should not be honored. UTF-8 will succeed in 
            // most cases.
            if( !hasMediaTypeHeader || Encoding.UTF7.Equals( mediaType.Encoding ) ) {
                return Encoding.UTF8;
            }
            return mediaType.Encoding;
        }
    }
}
