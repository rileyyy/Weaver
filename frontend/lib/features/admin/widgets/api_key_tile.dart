import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/admin/models/api_key.dart';
import 'package:weaver/shared/widgets/user_state_badge.dart';

/// One key: its label, scope, when it was created and last used, and
/// either a revoke button or when it was revoked.
class ApiKeyTile extends StatelessWidget {
  const ApiKeyTile({super.key, required this.apiKey, required this.onRevoke});

  final ApiKey apiKey;
  final VoidCallback onRevoke;

  @override
  Widget build(BuildContext context) {
    final small = Theme.of(context).textTheme.bodySmall;
    final lastUsedAt = apiKey.lastUsedAt;
    final revokedAt = apiKey.revokedAt;
    return ListTile(
      leading: const Icon(Icons.key),
      title: Text(
        apiKey.label,
        overflow: TextOverflow.ellipsis,
        style: apiKey.isRevoked
            ? TextStyle(color: Theme.of(context).disabledColor)
            : null,
      ),
      subtitle: Padding(
        padding: const EdgeInsets.only(top: 4),
        child: Wrap(
          spacing: 8,
          runSpacing: 4,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            UserStateBadge(label: apiKey.scope.label),
            if (revokedAt != null)
              UserStateBadge(
                label: 'Revoked ${formatDate(revokedAt)}',
                isWarning: true,
              ),
            Text('Created ${formatDate(apiKey.createdAt)}', style: small),
            Text(
              lastUsedAt == null
                  ? 'Never used'
                  : 'Last used ${formatDate(lastUsedAt)} '
                        '${formatTime(lastUsedAt)}',
              style: small,
            ),
          ],
        ),
      ),
      trailing: apiKey.isRevoked
          ? null
          : IconButton(
              tooltip: 'Revoke ${apiKey.label}',
              icon: const Icon(Icons.block),
              onPressed: onRevoke,
            ),
    );
  }
}
