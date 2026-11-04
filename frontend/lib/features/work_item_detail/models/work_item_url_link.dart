/// A link from a work item to something outside Weaver, such as a PR or a
/// quote.
class WorkItemUrlLink {
  const WorkItemUrlLink({required this.id, required this.url, this.label});

  factory WorkItemUrlLink.fromJson(Map<String, dynamic> json) =>
      WorkItemUrlLink(
        id: json['id'] as String,
        url: json['url'] as String,
        label: json['label'] as String?,
      );

  final String id;
  final String url;
  final String? label;

  String get displayName => label ?? url;

  static const Set<String> _allowedSchemes = {'http', 'https'};

  /// Why [url] can't be saved, or null if it can. The server enforces the
  /// same rule; checking here says so before a round trip.
  static String? urlError(String url) {
    final uri = Uri.tryParse(url.trim());
    final isValid =
        uri != null &&
        uri.isAbsolute &&
        _allowedSchemes.contains(uri.scheme) &&
        uri.host.isNotEmpty;
    return isValid ? null : 'Enter a full web address starting with https://.';
  }
}
