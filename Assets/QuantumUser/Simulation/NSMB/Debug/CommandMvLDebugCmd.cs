using Photon.Deterministic;

namespace Quantum {

    public class CommandMvLDebugCmd : DeterministicCommand {

        public DebugCommand CommandId;
        public AssetRef<EntityPrototype> SpawnData;
        public FPVector2 Position;

        public override void Serialize(BitStream stream) {
            if (stream.Writing) {
                stream.WriteByte((byte) CommandId);
            } else {
                CommandId = (DebugCommand) stream.ReadByte();
            }
            stream.Serialize(ref SpawnData);
            stream.Serialize(ref Position);
        }

        public enum DebugCommand : byte {
            SpawnEntity,
            KillSelf,
            FreezeSelf,
            // Moving the player has to come through the simulation like anything
            // else: a transform nudged from the view side is fought by physics
            // and thrown away on the next tick.
            Warp,
            ToggleNoclip,
        }
    }
}