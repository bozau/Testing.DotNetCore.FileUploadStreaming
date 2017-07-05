using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestFileUpload.Models
{
    public class FileUploadRequestResult
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public int Zipcode { get; set; }
        public string FilePath { get; set; }
    }
}
