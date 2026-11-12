import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/admin/space_history_view_model.dart';
import 'package:weaver/features/admin/widgets/admin_list_frame.dart';
import 'package:weaver/features/admin/widgets/space_history_entry_tile.dart';
import 'package:weaver/shared/models/space.dart';

/// Opens [space]'s history; completes when the page is closed.
Future<void> showSpaceHistoryPage(BuildContext context, Space space) =>
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => SpaceHistoryPage(
          space: space,
          viewModel: getIt<SpaceHistoryViewModel>(param1: space.id),
        ),
      ),
    );

/// Every change to one Space, newest first. Owns [viewModel] and disposes it
/// when closed.
class SpaceHistoryPage extends StatefulWidget {
  const SpaceHistoryPage({
    super.key,
    required this.space,
    required this.viewModel,
  });

  final Space space;
  final SpaceHistoryViewModel viewModel;

  @override
  State<SpaceHistoryPage> createState() => _SpaceHistoryPageState();
}

class _SpaceHistoryPageState extends State<SpaceHistoryPage> {
  SpaceHistoryViewModel get _viewModel => widget.viewModel;

  @override
  void initState() {
    super.initState();
    unawaited(_viewModel.load());
  }

  @override
  void dispose() {
    _viewModel.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('History of ${widget.space.name}')),
      body: ListenableBuilder(
        listenable: _viewModel,
        builder: (context, _) => AdminListFrame(
          isLoading: _viewModel.isLoading,
          loadError: _viewModel.loadError,
          onRetry: _viewModel.load,
          description:
              'When this Space was created, renamed or had its done-hiding '
              'period changed, and by whom. Changes to its work items are in '
              'each item’s own history.',
          child: _viewModel.entries.isEmpty
              ? const Padding(
                  padding: EdgeInsets.all(16),
                  child: Text('No changes recorded.'),
                )
              : ListView.separated(
                  padding: const EdgeInsets.only(bottom: 16),
                  itemCount: _viewModel.entries.length,
                  separatorBuilder: (_, _) => const Divider(height: 1),
                  itemBuilder: (context, index) =>
                      SpaceHistoryEntryTile(entry: _viewModel.entries[index]),
                ),
        ),
      ),
    );
  }
}
