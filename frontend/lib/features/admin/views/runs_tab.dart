import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/run_reports_view_model.dart';
import 'package:weaver/features/admin/widgets/admin_list_frame.dart';
import 'package:weaver/features/admin/widgets/run_tile.dart';

/// What each agent run changed, newest first: one collapsed row per run,
/// expanding to its changes (ADR-0010).
class RunsTab extends StatelessWidget {
  const RunsTab({super.key, required this.viewModel});

  final RunReportsViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: viewModel,
      builder: (context, _) => AdminListFrame(
        isLoading: viewModel.isLoading,
        loadError: viewModel.loadError,
        onRetry: viewModel.load,
        description:
            'What each agent run changed, newest first. Agents send a run '
            'id with their writes; expand a run to review them.',
        child: viewModel.runs.isEmpty
            ? const Padding(
                padding: EdgeInsets.all(16),
                child: Text(
                  'No runs yet. Writes an agent sends without a run '
                  'id are only in each item’s history.',
                ),
              )
            : ListView.separated(
                padding: const EdgeInsets.only(bottom: 16),
                itemCount: viewModel.runs.length + (viewModel.hasMore ? 1 : 0),
                separatorBuilder: (_, _) => const Divider(height: 1),
                itemBuilder: (context, index) {
                  if (index == viewModel.runs.length) return _loadMore();
                  final run = viewModel.runs[index];
                  return RunTile(
                    key: ValueKey(run.key),
                    run: run,
                    changes: viewModel.changesFor(run),
                    onExpand: () => unawaited(viewModel.loadChanges(run)),
                  );
                },
              ),
      ),
    );
  }

  Widget _loadMore() => Padding(
    padding: const EdgeInsets.all(16),
    child: Column(
      children: [
        if (viewModel.loadMoreError case final error?)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: Text(error),
          ),
        viewModel.isLoadingMore
            ? const CircularProgressIndicator()
            : OutlinedButton(
                onPressed: () => unawaited(viewModel.loadMore()),
                child: const Text('Load more'),
              ),
      ],
    ),
  );
}
