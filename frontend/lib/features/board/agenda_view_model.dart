import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/board/data/calendar_repository.dart';

/// The Agenda tab's own state: the server's today, which decides what's
/// Overdue and what's Today. The items come from `BoardViewModel`, which
/// already holds the shown Spaces and the header's filters.
@injectable
class AgendaViewModel extends ViewModel {
  AgendaViewModel(this._calendar);

  final CalendarRepository _calendar;

  DateTime? _today;
  String? _loadError;
  int _loadGeneration = 0;

  /// Null until the first load succeeds.
  DateTime? get today => _today;

  /// True until there's a date or an error to show, including before the
  /// first [load]; a reload keeps showing the last date.
  bool get isLoading => _today == null && _loadError == null;

  /// Set when there's no date to show because loading it failed. A failed
  /// reload keeps the last date rather than replacing the Agenda with this.
  String? get loadError => _loadError;

  /// Asks the server for today again. The Agenda reloads it on every visit,
  /// since the app can stay open past midnight.
  Future<void> load() async {
    final generation = ++_loadGeneration;
    _loadError = null;
    notifyIfActive();
    try {
      final today = await _calendar.today();
      if (generation != _loadGeneration) return;
      _today = today;
    } on ApiException {
      if (generation != _loadGeneration || _today != null) return;
      _loadError =
          'Could not load the Agenda. Check your connection and try again.';
    }
    notifyIfActive();
  }
}
