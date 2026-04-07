using System;
using System.Collections.Generic;
using System.Text;

namespace Extensions.ForeignEntity;

public interface IHasLogo
{
    int? LogoId { get; set; }
}

public interface IHasUser
{
    string? UserId { get; set; }
}

public interface IHasGroup
{
    int? GroupId { get; set; }
}
