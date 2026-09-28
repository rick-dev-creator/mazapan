package install

import "testing"

func TestParseSource(t *testing.T) {
	for in, want := range map[string][2]string{
		"https://github.com/a/b":        {"https://github.com/a/b", ""},
		"https://github.com/a/b#v1.2":   {"https://github.com/a/b", "v1.2"},
		"git@github.com:a/b#feature/x":  {"git@github.com:a/b", "feature/x"},
		"https://u@host/a/b":            {"https://u@host/a/b", ""},
		"/home/me/my@plugins/x#abc1234": {"/home/me/my@plugins/x", "abc1234"},
	} {
		s, r, err := ParseSource(in)
		if err != nil || s != want[0] || r != want[1] {
			t.Errorf("%s: got %q %q %v", in, s, r, err)
		}
	}
	for _, bad := range []string{"--upload-pack=x", "u#--output=x", "u#a..b", "u#a b", ""} {
		if _, _, err := ParseSource(bad); err == nil {
			t.Errorf("%q should be refused", bad)
		}
	}
}
