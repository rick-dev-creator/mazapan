package apply

import (
	"bytes"
	"encoding/json"
	"fmt"
	"regexp"
	"sort"
	"strings"
)

// Files shared with the app that uses them (qt6ct.conf, kdeglobals, a
// browser's Preferences) or with the person (Firefox's user.js, their
// userChrome.css): myarch only manages the keys it renders and leaves the
// rest alone. Formats: "ini"; "prefs" (user_pref("name", value); lines);
// "lines" (lines that must be there); "json" (leaves of an object).
//
// What myarch wrote last is kept in Owned as "<format>:" + those keys,
// instead of a hash of the whole file: a change is someone else's only
// when one of myarch's keys differs from what it wrote (or, for the
// person's own files, "prefs" and "lines", when it's gone).

// readable: can a file in this format be merged into without losing it?
// Only JSON can fail: a file that doesn't parse (a crash's half-write, a
// comment) is never rewritten: "unreadable", left as it is.
func readable(format string, text []byte) bool {
	if format != "json" {
		return true
	}
	var m map[string]any
	return json.Unmarshal(text, &m) == nil && m != nil
}

// removedIsEdit: in the person's own files, a key of myarch's that's gone
// was taken out on purpose (to opt a profile out): not put back silently.
// An app that rewrites its file may just drop it (Chromium does, while it
// runs): that's put back.
func removedIsEdit(format string) bool { return format == "prefs" || format == "lines" }

// dropKeys takes myarch's keys that it no longer manages (gone, as it wrote
// them) out of the file: a pref of an older version, the @import of a
// plugin that's disabled, a settings.json's color customizations (else an
// editor stays in a theme nothing manages). INI keeps them: an app like
// qt6ct needs its keys set.
func dropKeys(format, text string, drop map[string]string) string {
	if len(drop) == 0 || format == "ini" {
		return text
	}
	if format == "json" {
		return jsonDrop(text, drop)
	}
	lines := strings.Split(text, "\n")
	var out []string
	for _, line := range lines {
		key, value := "", ""
		switch format {
		case "prefs":
			if m := prefLine.FindStringSubmatch(line); m != nil {
				key, value = "\x00"+m[1], m[2]
			}
		case "lines":
			key, value = "\x00"+strings.TrimSpace(line), "1"
		}
		if v, ok := drop[key]; ok && v == value {
			continue
		}
		out = append(out, line)
	}
	return strings.Join(out, "\n")
}

// keysOf are a shared file's keys in its format: "section\x00key" -> value
// ("\x00name" for prefs and lines, "\x00a\x01b" for the JSON path a.b).
func keysOf(format, text string) map[string]string {
	switch format {
	case "prefs":
		return prefsKeys(text)
	case "lines":
		return linesKeys(text)
	case "json":
		return jsonKeys(text)
	}
	return iniKeys(text)
}

// mergeKeys sets want's keys in text, keeping everything else.
func mergeKeys(format, text string, want map[string]string) string {
	switch format {
	case "prefs":
		return prefsMerge(text, want)
	case "lines":
		return linesMerge(text, want)
	case "json":
		return jsonMerge(text, want)
	}
	return iniMerge(text, want)
}

// "json": myarch's keys are the leaves of the template's object, by path,
// its parts joined by \x01 (keys can hold dots: "workbench.colorTheme").
func jsonKeys(text string) map[string]string {
	var v any
	if json.Unmarshal([]byte(text), &v) != nil {
		return map[string]string{}
	}
	out := map[string]string{}
	var walk func(prefix string, v any)
	walk = func(prefix string, v any) {
		if m, ok := v.(map[string]any); ok && len(m) > 0 {
			for k, x := range m {
				walk(prefix+"\x01"+k, x)
			}
			return
		}
		b, _ := json.Marshal(v)
		out["\x00"+strings.TrimPrefix(prefix, "\x01")] = string(b)
	}
	walk("", v)
	return out
}

// jsonMerge sets want's paths in the object text holds (a new one when the
// file is empty; one that doesn't parse never gets here, see readable),
// creating the objects on the way.
func jsonMerge(text string, want map[string]string) string {
	root := map[string]any{}
	if strings.TrimSpace(text) != "" {
		if json.Unmarshal([]byte(text), &root) != nil || root == nil {
			return text // never rewritten blind: whatever it is stays
		}
	}
	for k, raw := range want {
		var val any
		if json.Unmarshal([]byte(raw), &val) != nil {
			continue
		}
		path := strings.Split(strings.TrimPrefix(k, "\x00"), "\x01")
		m := root
		for _, p := range path[:len(path)-1] {
			next, ok := m[p].(map[string]any)
			if !ok {
				next = map[string]any{}
				m[p] = next
			}
			m = next
		}
		m[path[len(path)-1]] = val
	}
	return jsonWrite(text, root)
}

// jsonWrite: laid out the way the file was, near enough: one line if it
// was one line (a browser's Preferences), else indented with tabs (a
// settings.json people read). No HTML escaping of <, >, &.
func jsonWrite(was string, root map[string]any) string {
	var b bytes.Buffer
	enc := json.NewEncoder(&b)
	enc.SetEscapeHTML(false)
	if strings.TrimSpace(was) == "" || strings.Contains(strings.TrimSpace(was), "\n") {
		enc.SetIndent("", "\t")
	}
	enc.Encode(root)
	if !strings.HasSuffix(was, "\n") && strings.TrimSpace(was) != "" {
		return strings.TrimSuffix(b.String(), "\n")
	}
	return b.String()
}

// jsonDrop deletes the paths still holding what myarch wrote there, and the
// objects that leaves empty.
func jsonDrop(text string, drop map[string]string) string {
	root := map[string]any{}
	if json.Unmarshal([]byte(text), &root) != nil || root == nil {
		return text
	}
	changed := false
	for k, v := range drop {
		path := strings.Split(strings.TrimPrefix(k, "\x00"), "\x01")
		var walk func(m map[string]any, i int) bool // true: m[path[i]] went
		walk = func(m map[string]any, i int) bool {
			if i == len(path)-1 {
				if cur, ok := m[path[i]]; ok {
					if b, _ := json.Marshal(cur); string(b) == v {
						delete(m, path[i])
						return true
					}
				}
				return false
			}
			next, ok := m[path[i]].(map[string]any)
			if !ok || !walk(next, i+1) {
				return false
			}
			if len(next) == 0 {
				delete(m, path[i])
			}
			return true
		}
		changed = walk(root, 0) || changed
	}
	if !changed {
		return text
	}
	return jsonWrite(text, root)
}

// "lines": myarch's keys are whole lines that must be in the file (an
// @import in someone's userChrome.css); a missing one goes at the top,
// where CSS wants its imports. Only the head of the file counts (up to the
// first line that isn't an @-rule or a comment): an @import after other
// rules is ignored by CSS, so it isn't there.
func linesKeys(text string) map[string]string {
	out := map[string]string{}
	inComment := false
	for _, line := range strings.Split(text, "\n") {
		t := strings.TrimSpace(line)
		switch {
		case inComment:
			inComment = !strings.Contains(t, "*/")
		case t == "":
		case strings.HasPrefix(t, "/*"):
			inComment = !strings.Contains(t, "*/")
		case strings.HasPrefix(t, "@"):
			out["\x00"+t] = "1"
		default:
			return out
		}
	}
	return out
}

func linesMerge(text string, want map[string]string) string {
	have := linesKeys(text)
	var missing []string
	for k := range want {
		if have[k] == "" {
			missing = append(missing, strings.TrimPrefix(k, "\x00"))
		}
	}
	sort.Strings(missing)
	if len(missing) == 0 {
		return text
	}
	return strings.Join(missing, "\n") + "\n" + text
}

// user_pref("name", value);  // a comment
var prefLine = regexp.MustCompile(`^\s*user_pref\s*\(\s*["']([^"']+)["']\s*,\s*(.*?)\s*\)\s*;(.*)$`)

// prefLines calls f for each user_pref line outside /* … */ comments (a
// commented-out pref isn't set).
func prefLines(lines []string, f func(i int, m []string)) {
	inComment := false
	for i, line := range lines {
		t := strings.TrimSpace(line)
		if inComment {
			inComment = !strings.Contains(t, "*/")
			continue
		}
		if strings.HasPrefix(t, "/*") {
			inComment = !strings.Contains(t, "*/")
			continue
		}
		if m := prefLine.FindStringSubmatch(line); m != nil {
			f(i, m)
		}
	}
}

func prefsKeys(text string) map[string]string {
	out := map[string]string{}
	prefLines(strings.Split(text, "\n"), func(_ int, m []string) {
		out["\x00"+m[1]] = m[2]
	})
	return out
}

// prefsMerge sets want's prefs where they are (keeping a trailing comment),
// else adds them at the end.
func prefsMerge(text string, want map[string]string) string {
	lines := strings.Split(strings.TrimRight(text, "\n"), "\n")
	if text == "" {
		lines = nil
	}
	done := map[string]bool{}
	prefLines(lines, func(i int, m []string) {
		if v, managed := want["\x00"+m[1]]; managed {
			lines[i] = fmt.Sprintf("user_pref(%q, %s);%s", m[1], v, m[3])
			done["\x00"+m[1]] = true
		}
	})
	var missing []string
	for k, v := range want {
		if !done[k] {
			missing = append(missing, fmt.Sprintf("user_pref(%q, %s);", strings.TrimPrefix(k, "\x00"), v))
		}
	}
	sort.Strings(missing)
	return strings.Join(append(lines, missing...), "\n") + "\n"
}

// iniKeys are an INI file's keys: "section\x00key" -> value. Keys before
// any section have section "".
func iniKeys(text string) map[string]string {
	out := map[string]string{}
	section := ""
	for _, line := range strings.Split(text, "\n") {
		t := strings.TrimSpace(line)
		switch {
		case t == "" || strings.HasPrefix(t, "#") || strings.HasPrefix(t, ";"):
		case strings.HasPrefix(t, "[") && strings.HasSuffix(t, "]"):
			section = t[1 : len(t)-1]
		default:
			if k, v, ok := strings.Cut(t, "="); ok {
				out[section+"\x00"+strings.TrimSpace(k)] = strings.TrimSpace(v)
			}
		}
	}
	return out
}

// sharedOwned is how Owned keeps a shared file's managed keys.
func sharedOwned(format string, keys map[string]string) string {
	var lines []string
	for k, v := range keys {
		lines = append(lines, k+"\x00"+v)
	}
	sort.Strings(lines)
	return format + ":" + strings.Join(lines, "\n")
}

// fromSharedOwned: the keys and format of a shared file's Owned entry; ok
// is false for a whole file's hash.
func fromSharedOwned(s string) (keys map[string]string, format string, ok bool) {
	for _, f := range []string{"ini", "prefs", "lines", "json"} {
		if strings.HasPrefix(s, f+":") {
			keys = map[string]string{}
			for _, line := range strings.Split(strings.TrimPrefix(s, f+":"), "\n") {
				parts := strings.SplitN(line, "\x00", 3)
				if len(parts) == 3 {
					keys[parts[0]+"\x00"+parts[1]] = parts[2]
				}
			}
			return keys, f, true
		}
	}
	return nil, "", false
}

// IsShared tells a shared file's Owned entry from a whole file's.
func IsShared(owned string) bool {
	_, _, ok := fromSharedOwned(owned)
	return ok
}

// Edited: has someone else changed the file since myarch wrote it (owned
// is what Owned has for it)? For a shared file, only its managed keys
// count.
func Edited(owned string, disk []byte) bool {
	old, format, shared := fromSharedOwned(owned)
	if !shared {
		return Sum(disk) != owned
	}
	have := keysOf(format, string(disk))
	for k, v := range old {
		hv, ok := have[k]
		if ok && hv != v || !ok && removedIsEdit(format) {
			return true
		}
	}
	return false
}

// RestoreShared puts a shared file's managed keys back as a backup had them
// (recorded: what Owned had for it then; current: what it has now; backup:
// the file then), keeping whatever the app wrote since. Owned from before
// the file was shared is a hash: then every key of the backup goes back.
func RestoreShared(disk []byte, recorded, current string, backup []byte) []byte {
	if _, format, ok := fromSharedOwned(current); ok && !readable(format, disk) {
		return disk // not something it can merge into: left as it is
	}
	keys, format, shared := fromSharedOwned(recorded)
	if !shared {
		_, format, _ = fromSharedOwned(current)
		keys = keysOf(format, string(backup))
	}
	return []byte(mergeKeys(format, string(disk), keys))
}

// iniMerge sets want's keys in text, in place where they are, else at the
// end of their section (created at the end of the file when missing).
// Everything else in text stays as it was.
func iniMerge(text string, want map[string]string) string {
	lines := strings.Split(strings.TrimRight(text, "\n"), "\n")
	if text == "" {
		lines = nil
	}
	done := map[string]bool{}
	section := ""
	lastInSection := map[string]int{} // section -> index of its last line
	for i, line := range lines {
		t := strings.TrimSpace(line)
		if strings.HasPrefix(t, "[") && strings.HasSuffix(t, "]") {
			section = t[1 : len(t)-1]
			lastInSection[section] = i
			continue
		}
		if t != "" {
			lastInSection[section] = i
		}
		k, _, ok := strings.Cut(t, "=")
		if !ok || strings.HasPrefix(t, "#") || strings.HasPrefix(t, ";") {
			continue
		}
		id := section + "\x00" + strings.TrimSpace(k)
		if v, managed := want[id]; managed {
			lines[i] = strings.TrimSpace(k) + "=" + v
			done[id] = true
		}
	}
	// Missing keys, grouped by section, in a stable order.
	missing := map[string][]string{}
	var sections []string
	for id, v := range want {
		if done[id] {
			continue
		}
		sec, k, _ := strings.Cut(id, "\x00")
		if missing[sec] == nil {
			sections = append(sections, sec)
		}
		missing[sec] = append(missing[sec], k+"="+v)
	}
	sort.Strings(sections)
	for _, sec := range sections {
		keys := missing[sec]
		sort.Strings(keys)
		if _, ok := lastInSection[sec]; !ok && sec == "" {
			// Keys outside any section go before the first one.
			lines = append(append([]string{}, keys...), lines...)
			for s, i := range lastInSection {
				lastInSection[s] = i + len(keys)
			}
			continue
		}
		if at, ok := lastInSection[sec]; ok {
			lines = append(lines[:at+1], append(keys, lines[at+1:]...)...)
			// Later sections moved down.
			for s, i := range lastInSection {
				if i > at {
					lastInSection[s] = i + len(keys)
				}
			}
			lastInSection[sec] = at + len(keys)
			continue
		}
		if len(lines) > 0 {
			lines = append(lines, "")
		}
		if sec != "" {
			lines = append(lines, "["+sec+"]")
		}
		lines = append(lines, keys...)
		lastInSection[sec] = len(lines) - 1
	}
	return strings.Join(lines, "\n") + "\n"
}
