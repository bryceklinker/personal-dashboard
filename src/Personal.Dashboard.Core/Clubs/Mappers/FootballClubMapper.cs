using AutoMapper;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Core.Leagues.Entities;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Core.Clubs.Mappers;

public class FootballClubMapper : Profile
{
    public FootballClubMapper()
    {
        CreateMap<FootballLeagueEntity, FootballClubLeagueModel>();
        CreateMap<FootballClubEntity, FootballClubModel>()
            .ForMember(d => d.Leagues, o => o.MapFrom(s => s.Leagues));
    }
}
