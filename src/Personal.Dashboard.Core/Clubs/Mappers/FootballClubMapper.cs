using AutoMapper;
using Personal.Dashboard.Core.Clubs.Entities;
using Personal.Dashboard.Models;

namespace Personal.Dashboard.Core.Clubs.Mappers;

public class FootballClubMapper : Profile
{
    public FootballClubMapper()
    {
        CreateMap<FootballClubEntity, FootballClubModel>();
    }
}
