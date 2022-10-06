using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
//using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using DevExtremeAspNetCoreApp1.Models;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.IO.Compression;
using System.Xml.Linq;
using System;
using static System.Net.WebRequestMethods;
using Microsoft.AspNetCore.Hosting;
using System.Runtime.InteropServices;

namespace DevExtremeAspNetCoreApp1.Controllers {
    [Route("api/[controller]")]
    public class DataController : Controller {
        private string rootFolderName = "RootFolder";

        [Route("GetItems")]
        [HttpGet]
        public string GetItems(string pathInfo) {
            var folderName = pathInfo == null ? rootFolderName : pathInfo;
            List<FileDataItem> items = new List<FileDataItem>();
            var directories = Directory.GetDirectories(folderName);
            foreach (var d in directories) {
                DirectoryInfo dinfo = new DirectoryInfo(d);
                items.Add(new FileDataItem() { Key = dinfo.FullName, IsDirectory = true, Created = dinfo.CreationTime, Name = dinfo.Name, HasSubDirectories = dinfo.GetDirectories().Length > 0 });
            }
            DirectoryInfo di = new DirectoryInfo(folderName);
            var files = di.GetFiles();
            foreach (FileInfo d in files) {
                FileInfo fi = new FileInfo(d.FullName);
                items.Add(new FileDataItem() { Key = fi.FullName, IsDirectory = false, Created = fi.CreationTime, Name = fi.Name, HasSubDirectories = false });
            }
            string response = this.CreateResponse(true, null, null, items.ToArray());
            return response;

        }

        [Route("CreateDirectory")]
        [HttpGet]
        public string CreateDirectory(string pathInfo, string name) {
            string response = string.Empty;
            if (pathInfo == null) {
                response = this.CreateResponse(false, 409, "You can't create a folder in the root directory", null);
            } else {
                var folderName = pathInfo;
                DirectoryInfo dir = Directory.CreateDirectory(pathInfo + "//" + name);
                if (dir != null) {
                    response = this.CreateResponse(true, null, null, null);
                }
            }
            return response;
        }

        [Route("DownloadItems/{name}")]
        public FileResult DownloadItems(string name) {
            if (string.IsNullOrEmpty(name))
                return null;
            var items = JsonConvert.DeserializeObject<string[]>(name);
            if (items.Length == 1) {
                var filePath = items[0];
                return File(new FileStream(filePath, FileMode.Open), "application/octet-stream");
            } else {
                byte[] compressedBytes;
                using (var resultStream = new MemoryStream()) {
                    //zip
                    using (var archive = new ZipArchive(resultStream, ZipArchiveMode.Create, true)) {
                        for (int i = 0; i < items.Length; i++) {
                            var filePath = items[i];
                            var fileInArchive = archive.CreateEntry(filePath, CompressionLevel.Optimal);
                            var fileBytes = System.IO.File.ReadAllBytes(filePath);
                            using (var entryStream = fileInArchive.Open())
                            using (var fileToCompressStream = new MemoryStream(fileBytes)) {
                                fileToCompressStream.CopyTo(entryStream);
                            }
                        }
                    }
                    compressedBytes = resultStream.ToArray();

                }

                return File(compressedBytes, "application/octet-stream");
            }

        }

        [Route("DeleteItem")]
        [HttpGet]
        public string DeleteItem(string pathInfo) {
            string response = string.Empty;
            FileAttributes attr = System.IO.File.GetAttributes(pathInfo);
            if (attr.HasFlag(FileAttributes.Directory)) {
                DirectoryInfo di = new DirectoryInfo(pathInfo);
                if (di.GetFiles().Length > 0 || di.GetDirectories().Length > 0) {
                    response = this.CreateResponse(false, 409, "You can't remove a directory with files or other directories", null);
                } else {
                    Directory.Delete(pathInfo);
                    response = this.CreateResponse(true, null, null, null);
                }
            } else {
                FileInfo di = new FileInfo(pathInfo);
                di.Delete();
                response = this.CreateResponse(true, null, null, null);
            }
            return response;
        }

        [Route("RenameItem")]
        [HttpGet]
        public string RenameItem(string pathInfo, string newName) {
            string response = string.Empty;
            FileAttributes attr = System.IO.File.GetAttributes(pathInfo);
            if (attr.HasFlag(FileAttributes.Directory)) {
                DirectoryInfo di = new DirectoryInfo(pathInfo);
                string folderName = Path.GetDirectoryName(pathInfo);
                string newDirectory = Path.Combine(di.Parent.FullName, newName);
                if (Directory.Exists(newDirectory)) {
                    return this.CreateResponse(false, 409, "The target directory already exists", null);
                }
                di.MoveTo(newDirectory);
            } else {
                FileInfo fi = new FileInfo(pathInfo);
                string newpath = Path.Combine(fi.Directory.FullName, newName);
                FileInfo nfi = new FileInfo(newpath);
                if (nfi.Exists) {
                    return this.CreateResponse(false, 409, "The target file already exists", null);
                }
                fi.MoveTo(newpath);
            }
            return this.CreateResponse(true, null, null, null); ;
        }
        string CreateResponse(bool success, int? errorCode, string errorText, FileDataItem[] result) {
            var serializerSettings = new JsonSerializerSettings();
            serializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            string response = JsonConvert.SerializeObject(new { success = success, errorCode = errorCode, errorText = errorText, result = result }, serializerSettings);
            return response;
        }


        void CopyDirectory(string sourceDir, string destinationDir) {            
            var dir = new DirectoryInfo(sourceDir);
            destinationDir = Path.Combine(destinationDir, dir.Name);
            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);
            foreach (FileInfo file in dir.GetFiles()) {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }
            foreach (DirectoryInfo subDir in dirs) {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }

        [Route("MoveItem")]
        [HttpGet]
        public string MoveItem(string pathInfo, string destinationDirectory) {
            if (string.IsNullOrEmpty(destinationDirectory)) {
                destinationDirectory = rootFolderName;
            }

            FileAttributes attr = System.IO.File.GetAttributes(pathInfo);
            if (attr.HasFlag(FileAttributes.Directory)) {
                var sourceDirectory = new DirectoryInfo(pathInfo);
                var targetDirectory = new DirectoryInfo(destinationDirectory);
                if (targetDirectory.GetDirectories().FirstOrDefault(d => d.Name == sourceDirectory.Name) != null) {
                    return this.CreateResponse(false, 409, "The dirctory already exists in the destination folder", null);

                } else {
                    CopyDirectory(pathInfo, destinationDirectory);
                    Directory.Delete(pathInfo, true);
                }

            } else {
                FileInfo fi = new FileInfo(pathInfo);
                string newpath = Path.Combine(destinationDirectory, fi.Name);
                FileInfo nfi = new FileInfo(newpath);
                if (nfi.Exists) {
                    return this.CreateResponse(false, 409, "The file already exists in the destination folder", null);
                }
                fi.MoveTo(newpath);
            }
            return this.CreateResponse(true, null, null, null); ;
        }

        [Route("CopyItem")]
        [HttpGet]
        public string CopyItem(string pathInfo, string destinationDirectory) {
            if (string.IsNullOrEmpty(destinationDirectory)) {
                destinationDirectory = rootFolderName;
            }

            FileAttributes attr = System.IO.File.GetAttributes(pathInfo);
            if (attr.HasFlag(FileAttributes.Directory)) {
                var sourceDirectory = new DirectoryInfo(pathInfo);
                var targetDirectory = new DirectoryInfo(destinationDirectory);
                if (sourceDirectory.GetDirectories().FirstOrDefault(d => d.Name == targetDirectory.Name) != null) {
                    return this.CreateResponse(false, 409, "The dirctory already exists in the destination folder", null);

                } else {
                    CopyDirectory(pathInfo, destinationDirectory);
                }

            } else {
                FileInfo fi = new FileInfo(pathInfo);
                string newpath = Path.Combine(destinationDirectory, fi.Name);
                FileInfo nfi = new FileInfo(newpath);
                if (nfi.Exists) {
                    return this.CreateResponse(false, 409, "The file already exists in the destination folder", null);
                }
                fi.CopyTo(newpath);
            }
            return this.CreateResponse(true, null, null, null); ;
        }

        [Route("UploadFileChunk")]
        [HttpPost]
        public string UploadChunk(IFormFile fileChunk) {
            if (Request.HasFormContentType) {
                Request.Form.TryGetValue("chunkMetadata", out var metadataSerialized);
                Request.Form.TryGetValue("destinationDirectory", out var destinationDirSerialized);
                var chunkMetadata = JsonConvert.DeserializeObject<ChunkMetadata>(metadataSerialized);
                var destinationDirectory = JsonConvert.DeserializeObject<FileDataItem>(destinationDirSerialized);

                SaveFile(fileChunk, chunkMetadata.fileName, Path.Combine(rootFolderName, destinationDirectory.Key));
                return this.CreateResponse(true, null, null, null); ;
            } else {
                return this.CreateResponse(false, 409, "An error occured!", null);
            }
        }

        [NonAction]
        void SaveFile(IFormFile file, string fileName, string destinationFolder) {
            var path = Path.Combine(destinationFolder, fileName);
            using (var tempFile = System.IO.File.Open(path, FileMode.Append)) {
                file.CopyTo(tempFile);
            }
        }
    }


}
