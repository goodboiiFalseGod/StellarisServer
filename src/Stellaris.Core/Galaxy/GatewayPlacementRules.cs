using Stellaris.Core.Content.Galaxy;

namespace Stellaris.Core.Galaxy;

/// <summary>Ограниченная локальная политика для точного abandoned gateway source effect.</summary>
internal static class GatewayPlacementRules
{
    public const string PolicyId = "GatewayOutsideOrbitalEnvelopeV1";

    public static (string Type, decimal AngleRange) Read(GalaxySyntaxValue effect)
    {
        if (effect.Kind != GalaxySyntaxKind.Block || effect.Entries.Count != 1 || effect.Entries[0].Key != "spawn_megastructure" ||
            effect.Entries[0].Operator != "=" || effect.Entries[0].Value.Kind != GalaxySyntaxKind.Block)
            throw new GalaxyStructureException("gateway-effect", "Поддерживается только один spawn_megastructure effect.", effect.Location);
        var fields = effect.Entries[0].Value;
        var type = GalaxyInitializerExecution.Scalar(fields, "type");
        var random = GalaxyInitializerExecution.Optional(fields, "random_pos");
        var angle = GalaxyInitializerExecution.Optional(fields, "orbit_angle");
        if (type != "gateway_ruined" || GalaxyInitializerExecution.Optional(fields, "type")?.Operator != "=" ||
            random?.Operator != "=" || random.Value.Kind != GalaxySyntaxKind.Scalar || random.Value.Text != "yes" ||
            angle?.Operator != "=" || angle.Value.Kind != GalaxySyntaxKind.Scalar || angle.Value.Text != "360" || fields.Entries.Count != 3)
            throw new GalaxyStructureException("gateway-effect", "Ожидался gateway_ruined random_pos=yes orbit_angle=360.", fields.Location);
        return (type, 360m);
    }
}
