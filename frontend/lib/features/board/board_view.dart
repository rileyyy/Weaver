import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/widgets/board_card.dart';

const double _laneLabelWidth = 160;
const double _columnWidth = 240;

class BoardView extends StatefulWidget {
  const BoardView({super.key});

  @override
  State<BoardView> createState() => _BoardViewState();
}

class _BoardViewState extends State<BoardView> {
  final BoardViewModel _viewModel = getIt<BoardViewModel>();

  @override
  void initState() {
    super.initState();
    unawaited(_viewModel.load());
    _viewModel.addListener(_showMoveErrorIfAny);
  }

  @override
  void dispose() {
    _viewModel
      ..removeListener(_showMoveErrorIfAny)
      ..dispose();
    super.dispose();
  }

  void _showMoveErrorIfAny() {
    final message = _viewModel.moveError;
    if (message == null) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
    _viewModel.clearMoveError();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Weaver')),
      body: ListenableBuilder(
        listenable: _viewModel,
        builder: (context, _) {
          if (_viewModel.isLoading) {
            return const Center(child: CircularProgressIndicator());
          }

          final loadError = _viewModel.loadError;
          if (loadError != null) {
            return _LoadErrorView(message: loadError, onRetry: _viewModel.load);
          }

          return SingleChildScrollView(
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _StatusHeaderRow(statuses: _viewModel.statuses),
                  for (final lane in _viewModel.swimlanes)
                    _SwimlaneRow(
                      swimlane: lane,
                      statuses: _viewModel.statuses,
                      onCardDropped: _viewModel.moveCard,
                    ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

class _LoadErrorView extends StatelessWidget {
  const _LoadErrorView({required this.message, required this.onRetry});

  final String message;
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            FilledButton(
              onPressed: () => unawaited(onRetry()),
              child: const Text('Retry'),
            ),
          ],
        ),
      ),
    );
  }
}

class _StatusHeaderRow extends StatelessWidget {
  const _StatusHeaderRow({required this.statuses});

  final List<BoardStatus> statuses;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        const SizedBox(width: _laneLabelWidth),
        for (final status in statuses)
          SizedBox(
            width: _columnWidth,
            child: Padding(
              padding: const EdgeInsets.all(8),
              child: Text(
                status.name,
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
          ),
      ],
    );
  }
}

class _SwimlaneRow extends StatelessWidget {
  const _SwimlaneRow({
    required this.swimlane,
    required this.statuses,
    required this.onCardDropped,
  });

  final Swimlane swimlane;
  final List<BoardStatus> statuses;
  final Future<void> Function(WorkItemCard card, String newStatusId)
  onCardDropped;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SizedBox(
              width: _laneLabelWidth,
              child: Padding(
                padding: const EdgeInsets.all(8),
                child: Text(
                  swimlane.title,
                  style: Theme.of(context).textTheme.titleSmall,
                ),
              ),
            ),
            for (final status in statuses)
              SizedBox(
                width: _columnWidth,
                child: _StatusColumn(
                  swimlane: swimlane,
                  status: status,
                  onCardDropped: onCardDropped,
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _StatusColumn extends StatelessWidget {
  const _StatusColumn({
    required this.swimlane,
    required this.status,
    required this.onCardDropped,
  });

  final Swimlane swimlane;
  final BoardStatus status;
  final Future<void> Function(WorkItemCard card, String newStatusId)
  onCardDropped;

  @override
  Widget build(BuildContext context) {
    final cards = swimlane.cards.where((c) => c.statusId == status.id);

    return DragTarget<WorkItemCard>(
      onWillAcceptWithDetails: (details) =>
          details.data.parentId == swimlane.parentId,
      onAcceptWithDetails: (details) =>
          unawaited(onCardDropped(details.data, status.id)),
      builder: (context, candidateData, rejectedData) {
        final colorScheme = Theme.of(context).colorScheme;
        return Container(
          margin: const EdgeInsets.all(4),
          padding: const EdgeInsets.all(4),
          constraints: const BoxConstraints(minHeight: 64),
          decoration: BoxDecoration(
            color: candidateData.isNotEmpty
                ? colorScheme.primaryContainer.withValues(alpha: 0.4)
                : colorScheme.surfaceContainerLow,
            borderRadius: BorderRadius.circular(8),
          ),
          child: Column(
            children: [for (final card in cards) BoardCard(card: card)],
          ),
        );
      },
    );
  }
}
