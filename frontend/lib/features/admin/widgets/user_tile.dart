import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/admin/models/admin_user.dart';
import 'package:weaver/features/admin/models/user_action.dart';
import 'package:weaver/features/admin/widgets/user_state_badge.dart';
import 'package:weaver/shared/models/user_kind.dart';

class UserTile extends StatelessWidget {
  const UserTile({
    super.key,
    required this.user,
    required this.isCurrentUser,
    required this.onAction,
  });

  final AdminUser user;
  final bool isCurrentUser;
  final ValueChanged<UserAction> onAction;

  @override
  Widget build(BuildContext context) {
    final lockedUntil = user.lockedUntil;
    final badges = [
      if (isCurrentUser) const UserStateBadge(label: 'You'),
      if (user.isAdmin) const UserStateBadge(label: 'Admin'),
      if (user.kind == UserKind.agent) const UserStateBadge(label: 'Agent'),
      if (!user.isActive)
        const UserStateBadge(label: 'Deactivated', isWarning: true),
      if (lockedUntil != null)
        UserStateBadge(
          label: 'Locked until ${formatTime(lockedUntil)}',
          isWarning: true,
        ),
    ];
    final initial = user.username.isEmpty ? '?' : user.username[0];
    return ListTile(
      leading: CircleAvatar(child: Text(initial.toUpperCase())),
      title: Text(
        user.username,
        overflow: TextOverflow.ellipsis,
        style: user.isActive
            ? null
            : TextStyle(color: Theme.of(context).disabledColor),
      ),
      subtitle: badges.isEmpty
          ? null
          : Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Wrap(spacing: 6, runSpacing: 4, children: badges),
            ),
      trailing: PopupMenuButton<UserAction>(
        tooltip: 'Actions for ${user.username}',
        onSelected: onAction,
        itemBuilder: (context) => [
          for (final action in UserAction.availableFor(
            user,
            isCurrentUser: isCurrentUser,
          ))
            PopupMenuItem(value: action, child: Text(action.label)),
        ],
      ),
    );
  }
}
