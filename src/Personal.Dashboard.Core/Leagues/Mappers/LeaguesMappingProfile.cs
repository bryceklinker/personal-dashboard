using AutoMapper;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Core.Leagues.Mappers;

public class LeaguesMappingProfile : Profile
{
    public LeaguesMappingProfile()
    {
        CreateMap<FootballLeagueSeason, FootballLeagueSeasonModel>();
        CreateMap<FootballLeagueEntity, FootballLeagueModel>()
            .ForMember(d => d.Seasons, o => o.MapFrom(s => s.Seasons));
    }
}
