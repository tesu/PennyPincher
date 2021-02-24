# Penny Pincher
![Downloads Badge](https://img.shields.io/endpoint?url=https%3A%2F%2Fvz32sgcoal.execute-api.us-east-1.amazonaws.com%2FPennyPincher)

XIVLauncher plugin for simplifying "pennying" (undercutting current lowest offer by 1).

The usual workflow for this is the following:
1. Open up the adjust price menu for the item you're undercutting
2. Click on the compare prices menu to view the current listings
3. Make sure none of them are your other retainers
4. Take a mental note of what the current lowest offer is
5. Subtract one from it
6. Close the current listings window
7. Update your item's price with the number you noted
8. Confirm your new price

This plugin eliminates the mental notekeeping steps in the middle by automatically copying the number you want onto your clipboard when you open the current listings window.

Therefore, your new workflow becomes this:
1. Open up the adjust price menu for the item you're undercutting
2. Click on the compare prices menu to view the current listings
3. Close the current listings window
4. Paste the undercut value in
5. Confirm your new price

This both speeds up the process and reduces room for error from typos/missing digits.

## Commands
| Command&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; | Description | Default |
|---------|-------------|---------|
|`/penny`|Toggles whether to always copy prices. Supercedes the `smart` setting. |disabled|
|`/penny delta <delta>`|Sets how much to undercut by. A delta of 0 would copy the same price as the lowest offer, and a delta of 100 would copy 100 under the lowest offer. Negative numbers work exactly how you would expect, though it's not obvious to me how this could be useful.|1|
|`/penny hq`|Toggles whether to only undercut high quality items. This setting does not apply to items with no high quality variant, where the cheapest normal quality item will always be undercut.|disabled|
|`/penny smart`|Toggles whether to always copy prices when accessing the marketboard from a retainer.|enabled|
|`/penny verbose`|Toggles whether to print to chat when a price to copied.|enabled|
|`/penny whitelist <add/remove> <retainerName> ...`|Adds/removes retainers from whitelist. Multiple retainer names can be provided by adding spaces between the names.|
|`/penny help`|Displays the list of commands.||

Note that `/penny alwayson` has been renamed to `/penny` as of version 1.1.0.0