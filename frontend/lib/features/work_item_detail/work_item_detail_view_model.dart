import 'package:injectable/injectable.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/auth/models/auth_user.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/work_item_detail.dart';
import 'package:weaver/features/work_item_detail/models/work_item_layer.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';

@injectable
class WorkItemDetailViewModel extends ViewModel {
  WorkItemDetailViewModel(this._repository);

  final WorkItemDetailRepository _repository;

  WorkItemDetail? _item;
  List<BoardStatus> _statuses = const [];
  List<WorkItemLayer> _layers = const [];
  List<AuthUser> _users = const [];
  bool _isLoading = true;
  String? _loadError;
  bool _isSaving = false;
  String? _saveError;

  WorkItemDetail? get item => _item;
  List<BoardStatus> get statuses => _statuses;
  List<WorkItemLayer> get layers => _layers;
  List<AuthUser> get users => _users;
  bool get isLoading => _isLoading;
  String? get loadError => _loadError;
  bool get isSaving => _isSaving;
  String? get saveError => _saveError;

  String? get statusName {
    for (final status in _statuses) {
      if (status.id == _item?.statusId) return status.name;
    }
    return null;
  }

  Future<void> load(String id) async {
    _isLoading = true;
    _loadError = null;
    notifyIfActive();

    try {
      final results = await Future.wait([
        _repository.getItem(id),
        _repository.loadStatuses(),
        _repository.loadLayers(),
        _repository.loadUsers(),
      ]);
      _item = results[0] as WorkItemDetail;
      _statuses = results[1] as List<BoardStatus>;
      _layers = results[2] as List<WorkItemLayer>;
      _users = results[3] as List<AuthUser>;
    } catch (_) {
      _loadError = 'Could not load this work item. Check your connection and try again.';
    } finally {
      _isLoading = false;
      notifyIfActive();
    }
  }

  Future<bool> saveDetails({
    required String title,
    required String? description,
    required String? layerId,
    required WorkItemPriority priority,
  }) => _save(() => _repository.updateDetails(
        _item!.id,
        title: title,
        description: description,
        layerId: layerId,
        priority: priority,
      ));

  Future<bool> saveAssignee(String? userId) => _save(() => _repository.assign(_item!.id, userId));

  Future<bool> saveSchedule(DateTime? startDate, DateTime? endDate) =>
      _save(() => _repository.reschedule(_item!.id, startDate, endDate));

  Future<bool> _save(Future<WorkItemDetail> Function() action) async {
    _isSaving = true;
    _saveError = null;
    notifyIfActive();

    try {
      _item = await action();
      return true;
    } catch (_) {
      _saveError = 'Could not save your change. Try again.';
      return false;
    } finally {
      _isSaving = false;
      notifyIfActive();
    }
  }
}
