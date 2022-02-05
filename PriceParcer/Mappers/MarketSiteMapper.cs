﻿using AutoMapper;
using Microsoft.AspNetCore.Mvc.Rendering;
using PriceParcer.Core.DTO;
using PriceParcer.Data;
using PriceParcer.Models;

namespace PriceParcer.Mappers
{
    public class MarketSiteMapper : Profile
    {
        public MarketSiteMapper()
        {
            //CreateMap<MarketSite, MarketSiteInProductViewModel>();
            CreateMap<MarketSite, MarketSiteDTO>();

            CreateMap<MarketSiteDTO, MarketSiteListItemViewModel>()
                .ForMember(dest => dest.CreatedByUserName,
                    opt => opt.MapFrom(src => src.CreatedByUser.UserName));
            CreateMap<MarketSiteDTO, MarketSiteDetailsViewModel>();
            CreateMap<MarketSiteDTO, MarketSiteCreateEditViewModel>();
            CreateMap<MarketSiteDTO, MarketSiteDeleteViewModel>();
            
            CreateMap<MarketSiteDTO, MarketSite>();
            CreateMap<MarketSiteDTO, MarketSiteInProductViewModel>();
            CreateMap<MarketSiteDTO, SelectListItem>()
                .ForMember(dest => dest.Text,
                    opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.Value,
                    opt => opt.MapFrom(src => src.Id));

            CreateMap<MarketSiteCreateEditViewModel, MarketSiteDTO>();
        }
    }
}