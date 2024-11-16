# Penny Pincher
![Downloads Badge](https://img.shields.io/endpoint?url=https%3A%2F%2Fvz32sgcoal.execute-api.us-east-1.amazonaws.com%2FPennyPincher)

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

This both speeds up the process and reduces room for error from typos.

## Changelog
1.8.0.1: API version bump  
1.8.0.0: API version bump  
1.7.0.1: API version bump  
1.7.0.0: Use hooks instead of opcodes  
1.6.0.1: Updated config options to enable/disable new behavior  
1.6.0.0: Automatically undercut HQ when listing HQ items (turn off HQ mode to use) and avoid undercutting your own retainers  
1.5.0.2: .NET 7 update  
1.5.0.1: .NET 6 update  
1.5.0.0: Cleaned up config options, and added new option to always undercut by a multiple  
1.4.1.2: Added back old "copy HQ price without holding Shift" mode as an option  
1.4.1.1: Hotfix for new HQ mode behavior  
1.4.1.0: New HQ mode behavior: hold Shift when opening marketboard to undercut HQ prices  
1.4.0.4: Fixed bug where a nonsense price was copied on error  
1.4.0.3: API version bump  
1.4.0.2: API version bump  
1.4.0.1: `/penny hq` returns, by popular demand  
1.4.0.0: Configuration via UI instead of slash-commands  
1.3.0.0: `/penny min` added, `/penny hq` behavior reverted to the old dumb behavior, updated to dalamud API v4  
1.2.0.1: `/penny mod` added, `/penny hq` is back (defaults to true so the smarter behavior is opt-out)  
1.2.0.0: `/penny hq` has been replaced with smarter behavior (checking if the item you're listing is HQ or not)  
1.1.0.0: `/penny alwayson` has been renamed to `/penny`
