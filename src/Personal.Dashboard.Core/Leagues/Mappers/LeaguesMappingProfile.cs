using AutoMapper;
using Personal.Dashboard.Core.Countries.Entities;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Core.Leagues.Mappers;

public class LeaguesMappingProfile : Profile
{
    public LeaguesMappingProfile()
    {
        CreateMap<FootballLeagueSeason, FootballLeagueSeasonModel>();
        CreateMap<FootballCountryEntity, FootballCountryModel>();
        CreateMap<FootballLeagueEntity, FootballLeagueModel>()
            .ForMember(d => d.Seasons, o => o.MapFrom(s => s.Seasons))
            .ForMember(d => d.Country, o => o.MapFrom(s => s.Country));
    }
}
