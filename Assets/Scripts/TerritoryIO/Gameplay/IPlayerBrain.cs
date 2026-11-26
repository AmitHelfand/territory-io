using UnityEngine;

namespace TerritoryIO.Gameplay
{
    /// <summary>
    /// Decides where a player wants to go. HumanInput reads keyboard / mouse / touch;
    /// BotBrain is the AI. A network brain could implement this later for online play.
    /// </summary>
    public interface IPlayerBrain
    {
        void OnSpawn(Player self);

        /// <summary>
        /// Called every frame. Return the direction the player wants to face (any length),
        /// or Vector2.zero to keep going straight. The player turns toward it at its turn rate.
        /// </summary>
        Vector2 DesiredHeading(Player self, GameManager game);

        /// <summary>Called each time the head enters a new grid cell. Good place for heavier thinking.</summary>
        void OnCellEntered(Player self, GameManager game);
    }
}
