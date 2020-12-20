using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Braavos.Core.Parsers;
using Braavos.Core.Parsers.DataObjects;
using Braavos.Core.Repositories;
using Braavos.Core.Repositories.DataObjects;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Braavos.Function
{
    public class BlobEntryPoint
    {
        private readonly IDataParser<CnNation> _dataParser;
        private readonly ICnDbRepository _cnDbRepository;
        private readonly IMapper _mapper;

        public BlobEntryPoint(IDataParser<CnNation> dataParser, ICnDbRepository cnDbRepository, IMapper mapper) => 
            (_dataParser, _cnDbRepository, _mapper) = (dataParser, cnDbRepository, mapper);

        [FunctionName(nameof(CnNationsImporter))]
        public async Task CnNationsImporter([BlobTrigger("nations/{name}", Connection = "AzureWebJobsStorage")]Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"CnNationsImporter trigger function processing blob... \n Name:{name} \n Size: {myBlob.Length} Bytes");

            // Convert CSV to data
            var allNationData = new List<CnNation>();
            try
            {
                await foreach (var fileRecord in _dataParser.Parse(myBlob))
                    allNationData.Add(fileRecord);
            }
            catch (Exception e)
            {
                log.LogError($"Critical error encountered while parsing blob. \n Message: {e.Message}\n StackTrace: {e.StackTrace}");
                return;
            }

            log.LogInformation($"File successfully parsed. {allNationData.Count} records found.");

            // Upload data to DB
            try
            {
                await _cnDbRepository.UpsertNations(allNationData.Select(_mapper.Map<Nation>).ToList(), name);
            }
            catch (Exception e)
            {
                log.LogError($"Critical error encountered while upserting nation data to the DB. \n Message: {e.Message}\n StackTrace: {e.StackTrace}");
                return;
            }

            log.LogInformation($"Data uploaded successfully.");

            // Move the file to the archive

            log.LogInformation($"{nameof(CnNationsImporter)} trigger function completed successfully!");
        }
    }
}
