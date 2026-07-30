using Photon.Deterministic;
using static Quantum.CommandMvLDebugCmd;

namespace Quantum {
    public unsafe class MvLDebugSystem : SystemMainThread {
        public override void Update(Frame f) {
            for (PlayerRef player = 0; player < f.MaxPlayerCount; player++) {
#if QUANTUM_3_1
                foreach (var cmd in f.GetPlayerCommands<CommandMvLDebugCmd>(player)) {
                    ExecuteCommand(f, player, cmd);
                }
#else
                if (f.GetPlayerCommand(player) is CommandMvLDebugCmd cmd) {
                    ExecuteCommand(f, player, cmd);
                }
#endif
            }

            FlyNoclippers(f);
        }

        // Nothing else moves a frozen body, so while noclip is on the player is
        // flown directly: their own input, no gravity, through walls.
        private void FlyNoclippers(Frame f) {
            foreach ((var entity, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                if (!f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                    || !physicsObject->IsFrozen
                    || !physicsObject->DisableCollision
                    || !mario->PlayerRef.IsValid) {
                    continue;
                }

                Input* input = f.GetPlayerInput(mario->PlayerRef);
                if (input == null) {
                    continue;
                }

                FPVector2 direction = FPVector2.Zero;
                if (input->Right) {
                    direction.X += 1;
                }
                if (input->Left) {
                    direction.X -= 1;
                }
                if (input->Up) {
                    direction.Y += 1;
                }
                if (input->Down) {
                    direction.Y -= 1;
                }
                if (direction == FPVector2.Zero) {
                    continue;
                }

                FP speed = input->Sprint ? 24 : 10;
                if (f.Unsafe.TryGetPointer(entity, out Transform2D* transform)) {
                    transform->Position += direction.Normalized * speed * f.DeltaTime;
                }
                mario->FacingRight = direction.X >= 0;
            }
        }

        private void ExecuteCommand(Frame f, PlayerRef player, CommandMvLDebugCmd cmd) {
            EntityRef marioEntity = EntityRef.None;
            MarioPlayer* mario = null;

            foreach ((var entity, var marioPtr) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                if (marioPtr->PlayerRef == player) {
                    marioEntity = entity;
                    mario = marioPtr;
                    break;
                }
            }

            if (!f.Exists(marioEntity)) {
                return;
            }

            switch (cmd.CommandId) {
            case DebugCommand.SpawnEntity:
                EntityRef newEntity = f.Create(cmd.SpawnData);
                if (f.Unsafe.TryGetPointer(newEntity, out Transform2D* newEntityTransform)) {
                    newEntityTransform->Position = f.Unsafe.GetPointer<Transform2D>(marioEntity)->Position;
                    newEntityTransform->Position.X += mario->FacingRight ? 1 : -1;
                }
                if (f.Unsafe.TryGetPointer(newEntity, out CoinItem* coinItem)) {
                    coinItem->InitializePlayerSpawn(f, newEntity, marioEntity);
                }
                if (f.Unsafe.TryGetPointer(newEntity, out Enemy* enemy)) {
                    enemy->DisableRespawning = true;
                    enemy->FacingRight = mario->FacingRight;
                    enemy->IsActive = true;
                    enemy->IsDead = false;
                }
                break;
            case DebugCommand.KillSelf:
                mario->Death(f, marioEntity, false, true, EntityRef.None);
                break;
            case DebugCommand.FreezeSelf:
                IceBlockSystem.Freeze(f, marioEntity);
                break;
            case DebugCommand.ToggleNoclip:
                // Frozen plus no collision is the state, rather than a new field
                // in their schema: PhysicsObjectSystem skips frozen bodies in
                // every one of its loops, so nothing integrates the player and
                // the position written below is the position they keep. An
                // ice-frozen player is frozen but still collides, so the pair
                // together cannot be mistaken for anything the game does.
                if (f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* noclipPhysics)) {
                    bool enabled = noclipPhysics->IsFrozen && noclipPhysics->DisableCollision;
                    noclipPhysics->IsFrozen = !enabled;
                    noclipPhysics->DisableCollision = !enabled;
                    noclipPhysics->Velocity = FPVector2.Zero;
                }
                break;
            case DebugCommand.Warp:
                if (f.Unsafe.TryGetPointer(marioEntity, out Transform2D* warpTransform)) {
                    warpTransform->Position = cmd.Position;
                }
                // Momentum carried through a warp fires you straight out of the
                // far side of wherever you landed.
                if (f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* warpPhysics)) {
                    warpPhysics->Velocity = FPVector2.Zero;
                }
                break;
            }
        }
    }
}