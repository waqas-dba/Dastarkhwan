global using System.Security.Claims;
global using System.Text;
global using System.Threading.RateLimiting;
global using Microsoft.AspNetCore.Authentication.JwtBearer;
global using Microsoft.AspNetCore.Authorization;
global using Microsoft.AspNetCore.Http;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Options;
global using Microsoft.IdentityModel.Tokens;
global using Microsoft.AspNetCore.Builder;


global using CoreKit.Identity.Common;
global using CoreKit.Identity.Constants;
global using CoreKit.Identity.Entities;
global using CoreKit.Identity.Interfaces;
global using CoreKit.Identity.Models;
global using CoreKit.Identity.Normalization;
global using CoreKit.Identity.Persistence;
global using CoreKit.Identity.Security;
global using CoreKit.Identity.Services;
global using CoreKit.Identity.Settings;
global using CoreKit.Identity.Validation;

global using Microsoft.AspNetCore.WebUtilities;
global using Microsoft.IdentityModel.JsonWebTokens;
global using System.Security.Cryptography;
global using System.Text.Json;
global using Microsoft.AspNetCore.Identity;

global using CoreKit.Identity.Authorization;
global using CoreKit.Identity.Hooks;
global using CoreKit.Identity.Seeding;