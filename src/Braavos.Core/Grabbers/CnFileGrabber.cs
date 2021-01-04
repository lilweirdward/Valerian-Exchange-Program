﻿using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace Braavos.Core.Grabbers
{
    public class CnFileGrabber : IDataGrabber
    {
        const string cnUrl = "https://www.cybernations.net";

        public async Task<(string FileName, Stream DataStream)> GetTodaysFileAsync(CnFileType fileType)
        {
            var dataStream = new MemoryStream();

            // Make sure we're using the CST representation of "now"
            var cstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cstTimeZone);

            var fileName = $"{cnFileNameFactory(fileType)}{now.Month}{now.Day}{now.Year}{GetCnFileExtension(fileType, now)}";

            using (var client = new HttpClient())
            {
                var message = new HttpRequestMessage(HttpMethod.Get, $"{cnUrl}/assets/{fileName}.zip");
                var response = await client.SendAsync(message);

                if (!response.IsSuccessStatusCode)
                    throw new Exception(await response.Content.ReadAsStringAsync());

                using (var responseData = await response.Content.ReadAsStreamAsync())
                using (var zip = new ZipArchive(responseData, ZipArchiveMode.Read))
                {
                    var usefulFile = zip.Entries[0].Open();
                    await usefulFile.CopyToAsync(dataStream);
                    dataStream.Position = 0;
                }
            }

            return (fileName, dataStream);
        }

        /// <summary>
        /// Factory method to get desired file name by desired file type
        /// </summary>
        private Func<CnFileType, string> cnFileNameFactory => fileType => fileType switch
        {
            CnFileType.Alliances => "CyberNations_SE_Alliance_Stats_",
            CnFileType.Nations => "CyberNations_SE_Nation_Stats_",
            CnFileType.Aid => "CyberNations_SE_Aid_Stats_",
            CnFileType.War => "CyberNations_SE_War_Stats_",
            _ => string.Empty
        };

        /// <summary>
        /// Factory method to get the "timestamp" extension based on the current time (i.e. hour)
        /// </summary>
        private string GetCnFileExtension(CnFileType fileType, DateTime now)
        {
            var firstPart = fileType switch
            {
                CnFileType.Alliances => "515",
                CnFileType.Nations => "510",
                CnFileType.Aid => "520",
                CnFileType.War => "525",
                _ => string.Empty
            };

            var lastPart = now switch
            {
                // "morning" file (i.e. after 6am CST but before 6pm CST)
                _ when now.Hour > 6 && now.Hour < 18 => "001",

                // if it's not the "morning" file then it has to be the "evening" file
                _ => "002"
            };

            return firstPart + lastPart;
        }
    }

    public enum CnFileType
    {
        Alliances,
        Nations,
        Aid,
        War
    }
}
