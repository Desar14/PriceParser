﻿using AutoMapper;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PriceParser.Core.DTO;
using PriceParser.Core.Interfaces;
using PriceParser.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceParser.Domain
{
    public class ParcingPricesService : IParsingPricesService
    {
        private readonly IMapper _mapper;
        private readonly IProductsFromSitesService _productsFromSitesService;
        private readonly IProductPricesService _productsPricesService;
        private readonly IMarketSitesService _marketSitesService;
        private readonly ILogger<ParcingPricesService> _logger;

        public ParcingPricesService(IMapper mapper, IProductsFromSitesService productsFromSitesService, IProductPricesService productsPricesService, IMarketSitesService marketSitesService, ILogger<ParcingPricesService> logger)
        {
            _mapper = mapper;
            _productsFromSitesService = productsFromSitesService;
            _productsPricesService = productsPricesService;
            _marketSitesService = marketSitesService;
            _logger = logger;
        }

        public async Task<ProductPriceDTO> ParseProductPriceAsync(Guid productFromSitesId)
        {

            if (productFromSitesId == Guid.Empty)
                throw new ArgumentNullException(nameof(productFromSitesId));
            var productFromSite = await _productsFromSitesService.GetDetailsAsync(productFromSitesId);

            return await ParseProductPriceAsync(productFromSite);
        }

        private static T GetParsedValueFromHtmlDocument<T>(HtmlDocument htmlDoc, string? nodePath, string? attributePath, string link)
        {

            var node = htmlDoc.DocumentNode.SelectSingleNode(nodePath);

            string? rawString = null;

            if (node != null)
            {
                if (!String.IsNullOrEmpty(attributePath))
                {
                    var attribute = node.Attributes.First(p => p.Name == attributePath);

                    if (attribute == null)
                    {
                        throw new ArgumentException($"Can't find value in path {nodePath} and attribute {attributePath} on link {link}: attribute not found");
                    }
                    else
                        rawString = attribute.Value;
                }
                else
                    rawString = node.InnerText;
            }
            else
            {
                throw new ArgumentException($"Can't find value in path {nodePath} on link {link}: path not found");
            }

            if (String.IsNullOrEmpty(rawString))
            {
                throw new ArgumentException($"Can't find value in path {nodePath}");
            }

            if (typeof(T) == typeof(string))
            {
                return (T)Convert.ChangeType(rawString, typeof(T));
            }
            else if (typeof(T) == typeof(double))
            {
                rawString = rawString.Replace(',', '.');
                rawString = rawString.Replace(" ", "");
                if (!Double.TryParse(rawString, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleValueParsed))
                {
                    throw new ArgumentException($"Can't parse price {rawString}");
                }
                return (T)Convert.ChangeType(doubleValueParsed, typeof(T));

            }
            else
                return default;
        }

        public async Task<bool> ParseSaveAllAvailablePricesAsync()
        {

            Dictionary<Guid, IEnumerable<ProductFromSitesDTO>> dataToParse = new();

            var sites = (await _marketSitesService.GetOnlyAvailableSitesAsync()).Select(item => item.Id);

            foreach (var item in sites)
            {
                var productsFromSite = await _productsFromSitesService.GetBySiteForParsingAsync(item);
                dataToParse.Add(item, productsFromSite);
            }

            ConcurrentDictionary<Guid, IEnumerable<ProductFromSitesDTO>> data = new(dataToParse);
            var parsedPrices = new ConcurrentBag<ProductPriceDTO>();

            await Parallel.ForEachAsync(data, async (currentElement, token) =>
            {
                foreach (var item in currentElement.Value)
                {
                    var dto = await ParseProductPriceAsync(item);
                    parsedPrices.Add(dto);
                }                
            });

            await _productsPricesService.AddProductPricesRangeAsync(parsedPrices);

            return true;
        }

        public async Task<bool> ParseSaveProductPriceAsync(Guid productFromSitesId)
        {
            var dto = await ParseProductPriceAsync(productFromSitesId);

            return await _productsPricesService.AddProductPriceAsync(dto);
        }

        public async Task<ProductPriceDTO> ParseProductPriceAsync(ProductFromSitesDTO productFromSite)
        {

            ProductPriceDTO result = null;            

            if (productFromSite.Site.ParseType == ParseTypes.Xpath)
            {
                result = new();

                var html = productFromSite.Path;

                HtmlWeb web = new HtmlWeb();

                var htmlDoc = web.Load(html);

                if (htmlDoc == null)
                {
                    throw new ArgumentException($"Can't load link {productFromSite.Path}");
                }
                double priceParsed = GetParsedValueFromHtmlDocument<double>(htmlDoc, productFromSite.Site.ParsePricePath, productFromSite.Site.ParsePriceAttributeName, productFromSite.Path);

                string CurrencyRawString;

                try
                {
                    CurrencyRawString = GetParsedValueFromHtmlDocument<string>(htmlDoc, productFromSite.Site.ParseCurrencyPath, productFromSite.Site.ParseCurrencyAttributeName, productFromSite.Path);
                }
                catch (Exception)
                {
                    //todo log
                    CurrencyRawString = "BYN";
                }

                result.FullPrice = priceParsed;
                result.ParseDate = DateTime.Now;
                result.Id = Guid.NewGuid();
                result.CurrencyCode = CurrencyRawString;
                result.ProductFromSiteId = productFromSite.Id;
            }

            return result;
        }
    }
}