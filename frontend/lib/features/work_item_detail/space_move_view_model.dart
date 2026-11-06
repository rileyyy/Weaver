import 'dart:async';

import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/network/parallel_api_errors.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/work_item_detail/data/space_move_repository.dart';
import 'package:weaver/features/work_item_detail/models/space_move_choices.dart';
import 'package:weaver/features/work_item_detail/models/space_move_plan.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/shared/data/work_item_conflict.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';

/// Moving a work item and its sub-items to another Space (ADR-0001). Picking
/// a Space loads the server's plan, whose defaults start as the choices; the
/// user can change any of them before confirming. The move is saved through
/// the shared [WorkItemStore], so it sends the version the dialog was based
/// on.
@injectable
class SpaceMoveViewModel extends ViewModel {
  SpaceMoveViewModel(this._repository, this._items);

  final SpaceMoveRepository _repository;
  final WorkItemStore _items;

  late WorkItem _item;
  List<Space> _spaces = const [];
  String? _targetSpaceId;
  SpaceMovePlan? _plan;
  List<WorkItemStatus> _targetStatuses = const [];
  List<WorkItemType> _targetTypes = const [];
  List<WorkItemPriority> _targetPriorities = const [];
  final Map<String, String> _statuses = {};
  final Map<String, String?> _types = {};
  final Map<String, String?> _priorities = {};
  WorkItem? _parent;
  bool _isLoading = false;
  // Bumped by each load, so one overtaken by a newer load (the user picked
  // another Space meanwhile) leaves the state alone.
  int _loadGeneration = 0;
  bool _isMoving = false;
  String? _error;

  /// The Spaces the item can move to: every one but its own.
  List<Space> get spaces => _spaces;
  String? get targetSpaceId => _targetSpaceId;

  /// Null until a Space is picked and its plan has loaded.
  SpaceMovePlan? get plan => _plan;
  List<WorkItemStatus> get targetStatuses => _targetStatuses;
  List<WorkItemType> get targetTypes => _targetTypes;
  List<WorkItemPriority> get targetPriorities => _targetPriorities;

  /// The item's new parent in the target Space; null for its top level.
  WorkItem? get parent => _parent;
  bool get isLoading => _isLoading;
  bool get isMoving => _isMoving;
  String? get error => _error;
  bool get canMove => _plan != null && !_isLoading && !_isMoving;

  String? statusChoice(String sourceId) => _statuses[sourceId];
  String? typeChoice(String sourceId) => _types[sourceId];
  String? priorityChoice(String sourceId) => _priorities[sourceId];

  Future<void> start(WorkItem item) {
    _item = item;
    return _load(() async {
      final spaces = await _repository.loadSpaces();
      return () => _spaces = [
        for (final space in spaces)
          if (space.id != item.spaceId) space,
      ];
    });
  }

  /// Loads the plan for [spaceId] and starts the choices from its defaults.
  /// A parent picked in another Space doesn't carry over.
  Future<void> selectSpace(String spaceId) {
    _targetSpaceId = spaceId;
    _plan = null;
    _parent = null;
    return _load(() async {
      final (plan, statuses, types, priorities) = await (
        _repository.planMove(_item.id, spaceId),
        _repository.loadStatuses(spaceId),
        _repository.loadTypes(spaceId),
        _repository.loadPriorities(spaceId),
      ).wait;
      return () => _apply(plan, statuses, types, priorities);
    });
  }

  void _apply(
    SpaceMovePlan plan,
    List<WorkItemStatus> statuses,
    List<WorkItemType> types,
    List<WorkItemPriority> priorities,
  ) {
    _plan = plan;
    _targetStatuses = statuses;
    _targetTypes = types;
    _targetPriorities = priorities;
    _statuses
      ..clear()
      ..addAll({for (final m in plan.statuses) m.sourceId: m.targetId!});
    _types
      ..clear()
      ..addAll({for (final m in plan.types) m.sourceId: m.targetId});
    _priorities
      ..clear()
      ..addAll({for (final m in plan.priorities) m.sourceId: m.targetId});
  }

  void chooseStatus(String sourceId, String targetId) =>
      _choose(() => _statuses[sourceId] = targetId);

  void chooseType(String sourceId, String? targetId) =>
      _choose(() => _types[sourceId] = targetId);

  void choosePriority(String sourceId, String? targetId) =>
      _choose(() => _priorities[sourceId] = targetId);

  void chooseParent(WorkItem? parent) => _choose(() => _parent = parent);

  void _choose(void Function() change) {
    change();
    notifyIfActive();
  }

  /// Items of the target Space matching [text], to pick the new parent from.
  Future<List<WorkItem>> searchParents(String text) async {
    final spaceId = _targetSpaceId;
    if (spaceId == null) return const [];
    return _repository.searchWorkItems(spaceId, text);
  }

  /// Sends every mapping shown, defaults included, so the move does what the
  /// dialog showed even if the server's defaults change meanwhile.
  Future<bool> move() async {
    final spaceId = _targetSpaceId;
    if (!canMove || spaceId == null) return false;
    final choices = SpaceMoveChoices(
      targetSpaceId: spaceId,
      parentId: _parent?.id,
      statuses: Map.of(_statuses),
      types: Map.of(_types),
      priorities: Map.of(_priorities),
    );

    _isMoving = true;
    _error = null;
    notifyIfActive();
    try {
      await _items.write(
        _item.id,
        send: (version) => _repository.moveToSpace(
          _item.id,
          choices,
          expectedVersion: version,
        ),
        reload: () => _repository.getItem(_item.id),
      );
      return true;
    } on WorkItemConflict {
      _error =
          'Someone else changed this work item, so it was not moved. '
          'Close this and try again.';
      return false;
    } on ApiException catch (e) {
      _error = switch (e) {
        // The server says which value or parent it refused.
        ApiException(statusCode: 400) ||
        ApiException(isConflict: true) => e.detail ?? e.message,
        _ => 'Could not move this work item. Try again.',
      };
      return false;
    } finally {
      _isMoving = false;
      notifyIfActive();
    }
  }

  /// [load] fetches and returns how to apply what it fetched, which is
  /// skipped if a newer load started meanwhile.
  Future<void> _load(Future<void Function()> Function() load) async {
    final generation = ++_loadGeneration;
    _isLoading = true;
    _error = null;
    notifyIfActive();
    String? error;
    void Function()? apply;
    try {
      apply = await load();
    } on ApiException {
      error = 'Could not load what the move needs. Try again.';
    } on ParallelWaitError<Object?, Object?> catch (e) {
      rethrowNonApiErrors(asyncErrorsOf(e.errors));
      error = 'Could not load what the move needs. Try again.';
    }
    if (generation != _loadGeneration) return;
    apply?.call();
    _error = error;
    _isLoading = false;
    notifyIfActive();
  }
}
