# ProtectedText
This is [protected text](https://www.protectedtext.com), anonymous text sharing service, non-official api for C# language.

About service you can read more [here](https://www.protectedtext.com/) and [here](https://www.protectedtext.com/site/helpusfight).

What can you do with this library?
## You can:
### **Create/connect to your own site**
```C#
ProtectedTextClient client=new("my site","site password");
 ```
Note: ProtectedTextClient is **IDisposable** type, so you must dispose it after using.
### **Read data from your site.** 
```C#
string my_data=await client.ReadSite();
```
Note: It reads all text, including tab's texts and separators

### Write site's text
```C#
await client.WriteSite("site's text");
```
Note: It **fully** rewrites site. Your current tabs will be deleted
### Change password for current site
```C#
await client.ChangePassword("new password");
```
### Delete current site
```C#
await client.DeleteSite();
```
### Read tabs text
```C#
string[] tabs=await client.ReadTabs();
```
### Push new tab to site
```C#
int tab_index = await client.PushTab("tab data");
```
### Remove tabs
```C#
await client.RemoveTab(tab_index);
```
### Edit tabs
```C#
await client.EditTab("new tab data", tab_index);
```
### Delete tabs
```C#
await client.DeleteTab(tab_index);
```
