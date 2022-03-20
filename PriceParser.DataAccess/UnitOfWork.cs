﻿using Microsoft.Extensions.Logging;
using PriceParser.Core;
using PriceParser.Core.Interfaces;
using PriceParser.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceParser.DataAccess
{
    public class UnitOfWork : IUnitOfWork
    {

        private readonly ApplicationDbContext _db;
        private readonly IRepository<MarketSite> _marketSitesRepo;
        private readonly IRepository<Product> _productRepo;
        private readonly IRepository<ProductFromSites> _productFromSitesRepo;
        private readonly IRepository<ProductPrice> _productPricesRepo;
        private readonly IRepository<UserReview> _userReviewsRepo;
        private readonly ILogger<UnitOfWork> _logger;

        public UnitOfWork(ApplicationDbContext db, IRepository<MarketSite> marketSitesRepo, IRepository<Product> productRepo, IRepository<ProductFromSites> productFromSitesRepo, IRepository<ProductPrice> productPricesRepo, IRepository<UserReview> userReviewsRepo, ILogger<UnitOfWork> logger)
        {
            _db = db;
            _marketSitesRepo = marketSitesRepo;
            _productRepo = productRepo;
            _productFromSitesRepo = productFromSitesRepo;
            _productPricesRepo = productPricesRepo;
            _userReviewsRepo = userReviewsRepo;
            _logger = logger;
        }

        public IRepository<MarketSite> MarketSites => _marketSitesRepo;

        public IRepository<Product> Products => _productRepo;

        public IRepository<ProductFromSites> ProductsFromSites => _productFromSitesRepo;

        public IRepository<ProductPrice> ProductPricesHistory => _productPricesRepo;

        public IRepository<UserReview> UserReviews => _userReviewsRepo;

        public async Task<int> Commit()
        {
            try
            {
                return await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unit of work commit error");
                throw;
            }            
        }

        public void Dispose()
        {
            _db?.Dispose();

            MarketSites.Dispose();
            Products.Dispose();
            ProductsFromSites.Dispose();
            ProductPricesHistory.Dispose();
            UserReviews.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}