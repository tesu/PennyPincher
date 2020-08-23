# Penny Helper
XIVLauncher plugin for simplifying "pennying" (undercutting current lowest offer by 1).

The usual workflow for this is the following:
1. Open up the adjust price menu for the item you're undercutting
2. Click on the compare prices menu to view the current listings
3. Take a mental note of what the current lowest offer is
4. Subtract one from it
5. Close the current listings window
6. Update your item's price with the number you noted
7. Confirm your new price

This plugin eliminates the mental notekeeping steps in the middle by automatically copying the number you want onto your clipboard when you open the current listings window.

Therefore, your new workflow becomes this:
1. Open up the adjust price menu for the item you're undercutting
2. Click on the compare prices menu to view the current listings
3. Close the current listings window
4. Paste the undercut value in
5. Confirm your new price

This both speeds up the process and reduces room for error from typos/missing digits.

## Commands
Penny Helper can be toggled on and off temporarily through `/penny`; the expected use case is to toggle this on right before a price adjustment session, and then toggling it off afterwards so you don't accidentally clobber your clipboard if you open up the marketboard later.

Penny Helper can be kept permanently on with the `alwayson` setting, which can be toggled with `/penny alwayson`. This setting is saved, and persists through game restarts.

Penny Helper can undercut (or overcut...?) by any integer, which you can set through `/penny delta <delta>`. The default value is 1, so a delta of 0 would copy the same price as the lowest offer, and a delta of 100 would copy 100 under that. Negative numbers work exactly how you would expect.

Penny Helper writes to the chat whenever it saves a new value to the clipboard by default. To toggle this setting, use `/penny verbose`.

You can review any of the existing commands by calling `/penny help`.
