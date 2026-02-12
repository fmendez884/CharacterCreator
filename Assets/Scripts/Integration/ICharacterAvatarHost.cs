using UnityEngine;

public interface ICharacterAvatarHost
{
    Transform AvatarAnchor { get; }
    void AttachAvatar(Transform avatarRoot);
}
