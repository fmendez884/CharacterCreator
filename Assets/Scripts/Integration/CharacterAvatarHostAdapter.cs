using UnityEngine;

[DisallowMultipleComponent]
public abstract class CharacterAvatarHostAdapter : MonoBehaviour, ICharacterAvatarHost
{
    public abstract Transform AvatarAnchor { get; }
    public abstract void AttachAvatar(Transform avatarRoot);
}
