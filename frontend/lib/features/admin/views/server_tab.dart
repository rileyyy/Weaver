import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/server_settings_admin_view_model.dart';
import 'package:weaver/features/admin/widgets/admin_list_frame.dart';
import 'package:weaver/features/admin/widgets/name_dialog.dart';
import 'package:weaver/shared/widgets/load_error_view.dart';

/// Settings that apply to the whole server, one row each.
class ServerTab extends StatelessWidget {
  const ServerTab({super.key, required this.viewModel});

  final ServerSettingsAdminViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: viewModel,
      builder: (context, _) {
        if (viewModel.isLoading) {
          return const Center(child: CircularProgressIndicator());
        }
        if (viewModel.loadError case final error?) {
          return LoadErrorView(message: error, onRetry: viewModel.load);
        }
        return Align(
          alignment: Alignment.topCenter,
          child: ConstrainedBox(
            constraints: const BoxConstraints(
              maxWidth: AdminListFrame.maxWidth,
            ),
            child: ListView(
              padding: const EdgeInsets.symmetric(vertical: 8),
              children: [_timeZoneTile(context)],
            ),
          ),
        );
      },
    );
  }

  Widget _timeZoneTile(BuildContext context) {
    final timeZoneId = viewModel.timeZoneId ?? '';
    return ListTile(
      title: const Text('Time zone'),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(timeZoneId),
          const Text(
            'Decides when “today” starts for everyone, e.g. when a '
            'repeating item’s next copy is due.',
          ),
        ],
      ),
      isThreeLine: true,
      trailing: IconButton(
        tooltip: 'Change time zone',
        icon: const Icon(Icons.edit_outlined),
        onPressed: () => unawaited(
          showNameDialog(
            context,
            title: 'Server time zone',
            label: 'IANA time zone, e.g. Europe/Berlin',
            submitLabel: 'Save',
            initialName: timeZoneId,
            onSave: viewModel.setTimeZone,
          ),
        ),
      ),
    );
  }
}
