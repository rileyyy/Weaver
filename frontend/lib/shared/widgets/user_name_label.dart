import 'package:flutter/material.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/user_kind.dart';
import 'package:weaver/shared/widgets/user_state_badge.dart';

/// A user's name as pickers and lists show it, with an Agent badge for
/// agent accounts so nobody hands work to an agent by mistake.
class UserNameLabel extends StatelessWidget {
  const UserNameLabel({super.key, required this.user});

  final User user;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Flexible(child: Text(user.username, overflow: TextOverflow.ellipsis)),
        if (user.kind == UserKind.agent) ...[
          const SizedBox(width: 8),
          const UserStateBadge(label: 'Agent'),
        ],
      ],
    );
  }
}
