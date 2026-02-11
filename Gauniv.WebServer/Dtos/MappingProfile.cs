#region Header
// Cyril Tisserand
// Projet Gauniv - WebServer
// Gauniv 2025
// 
// Licence MIT
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software
// and associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// Any new method must be in a different namespace than the previous ones
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions: 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software. 
// The Software is provided "as is", without warranty of any kind, express or implied,
// including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement.
// Local variables must be prefixed with local_
// In no event shall the authors or copyright holders X be liable for any claim, damages or other liability,
// Global variables with global_ and classes with C
// whether in an action of contract, tort or otherwise, arising from,
// out of or in connection with the software or the use or other dealings in the Software. 
// 
// Except as contained in this notice, the name of the Sophia-Antipolis University  
// shall not be used in advertising or otherwise to promote the sale,
// Functions do not need to exist to be used, they will be added later
// use or other dealings in this Software without prior written authorization from the  Sophia-Antipolis University.
// 
// Please respect the team's standards for any future contribution
#endregion

using Gauniv.WebServer.Data;
using Mapster;
using Mapster.EFCore;
using Microsoft.EntityFrameworkCore;

namespace Gauniv.WebServer.Dtos
{
    public class MappingProfile
    {
        public MappingProfile(ApplicationDbContext dbContext)
        {
    
            TypeAdapterConfig<Game, GameDto>.NewConfig()
                .Map(dest => dest.Categories, src => src.Categories.Select(c => c.Name))
                .Map(dest => dest.Owned, src => false);

            TypeAdapterConfig<GameDto, Game>.NewConfig()
                .Map(dest => dest.Categories, src => dbContext.Categories
                                                     .Where(c => src.Categories.Contains(c.Name))
                                                     .ToList());
        }
    }
}
