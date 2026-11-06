import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/admin/data/server_settings_admin_repository.dart';
import 'package:weaver/features/admin/models/admin_server_settings.dart';
import 'package:weaver/features/admin/state/admin_error_message.dart';

/// The Admin page's Server tab. Each change returns null on success, or why
/// it failed for the dialog that asked for it.
@injectable
class ServerSettingsAdminViewModel extends ViewModel {
  ServerSettingsAdminViewModel(this._repository);

  final ServerSettingsAdminRepository _repository;

  AdminServerSettings? _settings;
  bool _isLoading = false;
  String? _loadError;
  int _loadGeneration = 0;

  /// Null until the first load succeeds.
  String? get timeZoneId => _settings?.timeZoneId;

  /// Null until the first load succeeds.
  int? get trashRetentionDays => _settings?.trashRetentionDays;

  /// Only true for the first load; later refreshes keep showing the values.
  bool get isLoading => _isLoading && _settings == null;
  String? get loadError => _loadError;

  Future<void> load() async {
    final generation = ++_loadGeneration;
    _isLoading = true;
    _loadError = null;
    notifyIfActive();
    try {
      final settings = await _repository.get();
      if (generation != _loadGeneration) return;
      _settings = settings;
    } on ApiException {
      if (generation != _loadGeneration) return;
      _loadError = 'Could not load the server settings. Check your connection.';
    } finally {
      if (generation == _loadGeneration) {
        _isLoading = false;
        notifyIfActive();
      }
    }
  }

  Future<String?> setTimeZone(String timeZoneId) async {
    final trimmed = timeZoneId.trim();
    if (trimmed.isEmpty) return 'Enter a time zone.';
    try {
      _settings = await _repository.setTimeZone(trimmed);
      notifyIfActive();
      return null;
    } on ApiException catch (e) {
      return adminErrorMessage(e, 'Could not change the time zone. Try again.');
    }
  }

  /// [days] is the text the admin typed, checked here so a typo doesn't need
  /// a round trip.
  Future<String?> setTrashRetention(String days) async {
    final parsed = int.tryParse(days.trim());
    if (parsed == null || !_isValidRetention(parsed)) {
      return 'Enter a whole number of days from '
          '${AdminServerSettings.minTrashRetentionDays} to '
          '${AdminServerSettings.maxTrashRetentionDays}.';
    }
    try {
      _settings = await _repository.setTrashRetention(parsed);
      notifyIfActive();
      return null;
    } on ApiException catch (e) {
      return adminErrorMessage(
        e,
        'Could not change the Trash retention. Try again.',
      );
    }
  }

  static bool _isValidRetention(int days) =>
      days >= AdminServerSettings.minTrashRetentionDays &&
      days <= AdminServerSettings.maxTrashRetentionDays;
}
