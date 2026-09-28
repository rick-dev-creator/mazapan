package apply

import (
	"sort"
	"strings"
)

// Files shared with the app that uses them (qt6ct.conf, kdeglobals): the
// app writes its own keys there too (window geometry, recent files), so
// myarch only manages the keys it renders and leaves the rest alone.
//
// What myarch wrote last is kept in Owned as "ini:" + those keys, instead
// of a hash of the whole file: a change is someone else's only when one of
// myarch's keys differs from what it wrote.

const iniPrefix = "ini:"

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

// iniOwned is how Owned keeps a shared file's managed keys.
func iniOwned(keys map[string]string) string {
	var lines []string
	for k, v := range keys {
		lines = append(lines, k+"\x00"+v)
	}
	sort.Strings(lines)
	return iniPrefix + strings.Join(lines, "\n")
}

func fromIniOwned(s string) (map[string]string, bool) {
	if !strings.HasPrefix(s, iniPrefix) {
		return nil, false
	}
	out := map[string]string{}
	for _, line := range strings.Split(strings.TrimPrefix(s, iniPrefix), "\n") {
		parts := strings.SplitN(line, "\x00", 3)
		if len(parts) == 3 {
			out[parts[0]+"\x00"+parts[1]] = parts[2]
		}
	}
	return out, true
}

// IsShared tells a shared file's Owned entry from a whole file's.
func IsShared(owned string) bool { return strings.HasPrefix(owned, iniPrefix) }

// Edited: has someone else changed the file since myarch wrote it (owned
// is what Owned has for it)? For a shared file, only its managed keys
// count.
func Edited(owned string, disk []byte) bool {
	old, shared := fromIniOwned(owned)
	if !shared {
		return Sum(disk) != owned
	}
	have := iniKeys(string(disk))
	for k, v := range old {
		if hv, ok := have[k]; ok && hv != v {
			return true
		}
	}
	return false
}

// RestoreShared puts a shared file's managed keys back as a backup had them
// (recorded: what Owned had for it then; backup: the file then), keeping
// whatever the app wrote since. Owned from before files were shared is a
// hash: then every key of the backup is put back.
func RestoreShared(disk []byte, recorded string, backup []byte) []byte {
	keys, shared := fromIniOwned(recorded)
	if !shared {
		keys = iniKeys(string(backup))
	}
	return []byte(iniMerge(string(disk), keys))
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
